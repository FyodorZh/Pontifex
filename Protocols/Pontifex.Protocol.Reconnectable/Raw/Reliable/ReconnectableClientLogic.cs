using System;
using Actuarius.Concurrent;
using Actuarius.Memory;
using Operarius;
using Pontifex.StopReasons;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Raw.Reliable.Reconnectable
{
    /// <summary>
    /// The persistent client half of a reconnectable session. It owns the delivery system, the session
    /// identity, and the endpoint exposed to the application, and starts one negotiation over every fresh
    /// inner transport produced by <see cref="_underlyingTransportFactory"/>.
    /// </summary>
    internal sealed class ReconnectableClientLogic : ReconnectableBaseLogic<IRawReliableEndpoint>, IRawReliableEndpoint
    {
        private readonly Func<IRawReliableClient?> _underlyingTransportFactory;
        private readonly IRawReliableClientHandler _userHandler;
        private readonly short _negotiationVersion;
        private readonly string _negotiationName;
        private readonly int _handshakeMaxByteSize;
        private readonly ITimerFactory _timerFactory;
        private readonly Func<DateTime> _now;

        private readonly ThreadSafeDateTime _nextReconnectionTime = new ThreadSafeDateTime();

        private IMultiRefReadOnlyByteArray? _secret;

        public event Action<IRawReliableEndpoint>? OnConnected;

        public SessionId SessionId => _sessionId;

        internal ReconnectableClientLogic(
            Func<IRawReliableClient?> underlyingTransportFactory,
            IRawReliableClientHandler userHandler,
            TimeSpan disconnectTimeout,
            short negotiationVersion,
            string negotiationName,
            int handshakeMaxByteSize,
            ITimerFactory? timerFactory,
            Func<DateTime>? now,
            ILogger logger,
            IMemoryRental memoryRental)
            : base(userHandler, disconnectTimeout, logger, memoryRental)
        {
            _underlyingTransportFactory = underlyingTransportFactory ?? throw new ArgumentNullException(
                nameof(underlyingTransportFactory));
            _userHandler = userHandler ?? throw new ArgumentNullException(nameof(userHandler));
            _negotiationVersion = negotiationVersion;
            _negotiationName = negotiationName ?? throw new ArgumentNullException(nameof(negotiationName));
            _handshakeMaxByteSize = handshakeMaxByteSize;
            _timerFactory = timerFactory ?? SystemTimerFactory.Instance;
            _now = now ?? (() => DateTime.UtcNow);

            _nextReconnectionTime.Time = _now();
        }

        protected override bool BeginReconnect()
        {
            if (_nextReconnectionTime.Time > _now())
            {
                return false;
            }

            IRawReliableClient? transport = _underlyingTransportFactory.Invoke();
            if (transport == null)
            {
                return false;
            }

            var attempt = new ClientAttemptHandler(this, _negotiationVersion, _negotiationName,
                _handshakeMaxByteSize, _timerFactory);
            if (!transport.Init(attempt))
            {
                return false;
            }

            _nextReconnectionTime.Time = _now().AddSeconds(ReconnectableInfo.ReconnectionPeriod.TotalSeconds);
            return transport.Start(r =>
            {
                Log.i("Reconnection transport stopped: {@reason}", r.Print());
            });
        }

        public override void OnDisconnected(StopReason reason)
        {
            _nextReconnectionTime.Time = _now().AddSeconds(ReconnectableInfo.ReconnectionPeriod.TotalSeconds);
        }

        public override string ToString()
        {
            return "reconnectable-client[" + (_sessionId.IsValid ? _sessionId.ToString() : "invalid-session") +
                   "]";
        }

        internal void OnAttemptReceived(UnionDataList receivedBuffer)
        {
            ((IRawHandler)this).OnReceived(receivedBuffer);
        }

        internal void OnAttemptDisconnected(StopReason reason)
        {
            OnDisconnected(reason);
        }

        internal void OnAttemptStopped(StopReason reason)
        {
            ((IRawHandler)this).OnStopped(reason);
        }

        internal void RejectAttempt(StopReason reason)
        {
            Fail(reason);
        }

        /// <summary>
        /// Appends the reconnectable custom tail to the negotiation request. A first connection announces an
        /// invalid session id and an empty secret; a reconnect announces the live session id and secret.
        /// </summary>
        internal void FillNegotiationData(UnionDataList customTail)
        {
            if (_sessionId.IsValid)
            {
                if (_secret == null)
                {
                    throw new InvalidOperationException(
                        "The session secret must be set before a reconnect request is sent.");
                }

                customTail.PutLast(_sessionId.Id);
                customTail.PutLast(_sessionId.Generation);
                customTail.PutLast(new UnionData(_secret.Acquire()));
                return;
            }

            customTail.PutLast(SessionId.Invalid.Id);
            customTail.PutLast(SessionId.Invalid.Generation);
            customTail.PutLast(new UnionData(new MultiRefByteArray(Array.Empty<byte>())));
        }

        internal void OnNegotiationAccepted(IRawReliableEndpoint endPoint, UnionDataList customTail)
        {
            if (!customTail.TryPopFirst(out int id) || !customTail.TryPopFirst(out int generation))
            {
                Fail("Disconnecting due to a wrong reconnectable negotiation response");
                return;
            }

            if (!customTail.TryPopFirst(out IMultiRefReadOnlyByteArray? secret))
            {
                Fail("Disconnecting due to a missing session secret in the negotiation response");
                return;
            }

            using var secretDisposer = secret.AsDisposable();

            SessionId sessionId = new SessionId(id, generation);
            if (!sessionId.IsValid)
            {
                Fail("Disconnecting due to an incorrect session id");
                return;
            }

            if (_sessionId.IsValid && !_sessionId.Equals(sessionId))
            {
                Fail("Disconnecting due to session id mismatch. Expected " + _sessionId + ", received " +
                     sessionId);
                return;
            }

            _secret?.Release();
            _secret = secret.Acquire();
            _sessionId = sessionId;

            Connect(endPoint, out bool isFirstConnection);
            if (isFirstConnection)
            {
                Log.i("Logic connected");
                OnConnected?.Invoke(this);
            }
        }

        internal void OnNegotiationRejected(StopReason reason)
        {
            if (reason is ProtocolMismatch || reason is HandshakeRejected)
            {
                Stop(reason);
                return;
            }

            OnConnectionStopped(reason);
        }

        protected override void OnInnerStopped(StopReason reason)
        {
            if (reason is ProtocolMismatch || reason is HandshakeRejected)
            {
                Stop(reason);
            }
            else
            {
                OnConnectionStopped(reason);
            }
        }

        private sealed class ClientAttemptHandler : IRawReliableClientHandler
        {
            private readonly ReconnectableClientLogic _logic;
            private readonly short _version;
            private readonly string _name;
            private readonly int _handshakeMaxByteSize;
            private readonly ITimerFactory _timerFactory;

            private IRawReliableEndpoint? _endpoint;
            private NegotiationSession? _negotiation;
            private volatile bool _admitted;

            public ClientAttemptHandler(ReconnectableClientLogic logic, short version, string name,
                int handshakeMaxByteSize, ITimerFactory timerFactory)
            {
                _logic = logic;
                _version = version;
                _name = name;
                _handshakeMaxByteSize = handshakeMaxByteSize;
                _timerFactory = timerFactory;
            }

            public void OnConnected(IRawReliableEndpoint endPoint)
            {
                _endpoint = endPoint;

                var negotiation = new NegotiationSession(
                    NegotiationRole.Client,
                    _version,
                    _name,
                    SendEnvelope,
                    OnRejected,
                    reason => _endpoint?.Disconnect(reason),
                    OnAccepted,
                    appendData: _logic.FillNegotiationData,
                    handshakeMaxByteSize: _handshakeMaxByteSize,
                    timerFactory: _timerFactory,
                    log: _logic.Log);

                _negotiation = negotiation;
                negotiation.Start();
            }

            public void OnReceived(UnionDataList receivedBuffer)
            {
                if (_admitted)
                {
                    _logic.OnAttemptReceived(receivedBuffer);
                    return;
                }

                var negotiation = _negotiation;
                if (negotiation != null && negotiation.OnReceived(receivedBuffer))
                {
                    return;
                }

                receivedBuffer.Release();
                _logic.RejectAttempt(new ProtocolMismatch(ReconnectableInfo.TransportName,
                    "unexpected payload before the reconnectable negotiation completed"));
                _endpoint?.Disconnect(new ProtocolMismatch(ReconnectableInfo.TransportName,
                    "unexpected payload before the reconnectable negotiation completed"));
            }

            public void OnDisconnected(StopReason reason)
            {
                _logic.OnAttemptDisconnected(reason);
            }

            public void OnStopped(StopReason reason)
            {
                var negotiation = _negotiation;
                _negotiation = null;
                negotiation?.Dispose();
                _logic.OnAttemptStopped(reason);
            }

            private void OnAccepted(UnionDataList customTail)
            {
                _admitted = true;
                _logic.OnNegotiationAccepted(_endpoint!, customTail);
            }

            private void OnRejected(StopReason reason)
            {
                _logic.OnNegotiationRejected(reason);
                _endpoint?.Disconnect(reason);
            }

            private SendResult SendEnvelope(UnionDataList envelope)
            {
                var endpoint = _endpoint;
                if (endpoint == null)
                {
                    envelope.Release();
                    return SendResult.NotConnected;
                }

                try
                {
                    return endpoint.Send(envelope);
                }
                catch (Exception ex)
                {
                    _logic.Log.wtf(ex);
                    return SendResult.Error;
                }
            }
        }
    }
}
