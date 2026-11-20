using System;
using System.Collections.Generic;
using System.Threading;
using Actuarius.Memory;
using Pontifex.StopReasons;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Raw.Reliable.Protocols
{
    /// <summary>
    /// Base reliable client wrapper: negotiates a layer dataset over one <see cref="IRawReliableClient"/> and
    /// forwards application traffic only after the negotiation is admitted.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Admission gate</b>: the wrapper starts the negotiation when the inner transport
    /// reports <c>OnConnected</c>. The user handler sees <c>OnConnected</c> only after the inner negotiation
    /// succeeds; before that, <c>Send</c> is not routed to the inner transport and inbound application data is
    /// never delivered.</description></item>
    /// <item><description><b>Lifecycle</b>: an admitted session observes <c>OnConnected</c> →
    /// <c>OnDisconnected</c> → <c>OnStopped</c> exactly once. A session that was never admitted observes
    /// exactly one <c>OnStopped</c> and no <c>OnDisconnected</c>.</description></item>
    /// <item><description><b>Ownership</b>: the wrapper owns the negotiation envelopes. The logic hooks receive
    /// only the custom tail and must not retain or release it.</description></item>
    /// </list>
    /// </remarks>
    public class RawReliableWrapperClient : RawReliableClientTransport<IRawReliableClientHandler>, IRawReliableClient
    {
        private readonly IRawReliableClient _inner;
        private readonly short _version;
        private readonly string _negotiationName;
        private readonly int _handshakeMaxByteSize;
        private readonly TimeSpan _rejectLinger;
        private readonly INegotiationClock _clock;

        private IRawReliableWrapperLogic? _logic;
        private IRawReliableClientHandler? _userHandler;
        private ClientSession? _session;

        /// <summary>
        /// Initializes a wrapper client.
        /// </summary>
        /// <param name="typeName">The display name of the wrapper transport.</param>
        /// <param name="negotiationName">The layer negotiation name. Must be non-null.</param>
        /// <param name="inner">The wrapped reliable client. Must be non-null.</param>
        /// <param name="logic">The layer logic. Must be non-null.</param>
        /// <param name="version">The layer negotiation version.</param>
        /// <param name="handshakeMaxByteSize">The inclusive maximum negotiation envelope size.</param>
        /// <param name="rejectLinger">How long a rejected session stays open, or <see langword="null"/> for the
        /// two-second default.</param>
        public RawReliableWrapperClient(string typeName, string negotiationName, IRawReliableClient inner,
            IRawReliableWrapperLogic logic, short version = RawReliableNegotiation.DefaultVersion,
            int handshakeMaxByteSize = RawReliableNegotiation.DefaultMaxByteSize,
            TimeSpan? rejectLinger = null)
            : this(typeName, negotiationName, inner, inner.Log, inner.Memory, version, handshakeMaxByteSize,
                rejectLinger, null)
        {
            SetupLogic(logic);
        }

        /// <summary>
        /// Initializes a wrapper client whose logic is supplied by a derived type through
        /// <see cref="SetupLogic"/>.
        /// </summary>
        protected RawReliableWrapperClient(string typeName, string negotiationName, IRawReliableClient inner,
            ILogger logger, IMemoryRental memory, short version = RawReliableNegotiation.DefaultVersion,
            int handshakeMaxByteSize = RawReliableNegotiation.DefaultMaxByteSize,
            TimeSpan? rejectLinger = null)
            : this(typeName, negotiationName, inner, logger, memory, version, handshakeMaxByteSize, rejectLinger,
                null)
        {
        }

        /// <summary>
        /// Initializes a wrapper client with an explicit time seam. Intended for deterministic tests.
        /// </summary>
        internal RawReliableWrapperClient(string typeName, string negotiationName, IRawReliableClient inner,
            IRawReliableWrapperLogic logic, short version, int handshakeMaxByteSize, TimeSpan? rejectLinger,
            INegotiationClock? clock)
            : this(typeName, negotiationName, inner, inner.Log, inner.Memory, version, handshakeMaxByteSize,
                rejectLinger, clock)
        {
            SetupLogic(logic);
        }

        private RawReliableWrapperClient(string typeName, string negotiationName, IRawReliableClient inner,
            ILogger logger, IMemoryRental memory, short version, int handshakeMaxByteSize,
            TimeSpan? rejectLinger, INegotiationClock? clock)
            : base(typeName, logger, memory, new RawReliableConformanceControl())
        {
            if (negotiationName == null)
            {
                throw new ArgumentNullException(nameof(negotiationName));
            }

            if (inner == null)
            {
                throw new ArgumentNullException(nameof(inner));
            }

            _inner = inner;
            _version = version;
            _negotiationName = negotiationName;
            _handshakeMaxByteSize = handshakeMaxByteSize;
            _rejectLinger = rejectLinger ?? RawReliableNegotiation.DefaultRejectLinger;
            _clock = clock ?? NegotiationClock.Shared;
        }

        /// <inheritdoc/>
        public override TransportType Type => TransportType.RawReliable;

        /// <inheritdoc/>
        public override int MessageMaxByteSize => _inner.MessageMaxByteSize;

        /// <summary>
        /// Sets the layer logic once, before the transport is started.
        /// </summary>
        protected void SetupLogic(IRawReliableWrapperLogic logic)
        {
            if (logic == null)
            {
                throw new ArgumentNullException(nameof(logic));
            }

            lock (_locker)
            {
                if (HasStartBeenAttempted)
                {
                    throw new InvalidOperationException("The wrapper logic must be set before start.");
                }

                _logic = logic;
            }
        }

        /// <inheritdoc/>
        protected override IRawReliableClientHandler? SetupHandler(IRawReliableClientHandler handler)
        {
            if (handler == null)
            {
                return null;
            }

            _userHandler = handler;
            return new TerminalHandler(this);
        }

        /// <inheritdoc/>
        protected override bool BeginConnect()
        {
            var logic = _logic;
            var userHandler = _userHandler;
            if (logic == null || userHandler == null)
            {
                Log.e("Wrapper logic or handler is not set");
                return false;
            }

            var session = new ClientSession(this, logic, userHandler);
            _session = session;

            try
            {
                if (!_inner.Init(session))
                {
                    _session = null;
                    return false;
                }

                return _inner.Start(OnInnerStopped);
            }
            catch (Exception ex)
            {
                Log.wtf(ex);
                _session = null;
                return false;
            }
        }

        /// <inheritdoc/>
        protected override void DestroyTransport(StopReason reason)
        {
            var session = _session;
            _session = null;
            session?.DisposeSession();

            try
            {
                _inner.Stop(reason);
            }
            catch (Exception ex)
            {
                Log.wtf(ex);
            }
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"{Name}<{_inner}>";
        }

        private void OnInnerStopped(StopReason reason)
        {
            if (IsStarted)
            {
                Stop(reason);
            }
        }

        private void StopFromSession(StopReason reason)
        {
            Stop(reason);
        }

        private sealed class TerminalHandler : IRawReliableClientHandler
        {
            private readonly RawReliableWrapperClient _wrapper;

            public TerminalHandler(RawReliableWrapperClient wrapper)
            {
                _wrapper = wrapper;
            }

            public void OnConnected(IRawReliableEndpoint endPoint)
            {
                // The terminal handler is never the inner transport handler.
            }

            public void OnReceived(UnionDataList receivedBuffer)
            {
                receivedBuffer.Release();
            }

            public void OnDisconnected(StopReason reason)
            {
                _wrapper._session?.RaiseDisconnected(reason);
            }

            public void OnStopped(StopReason reason)
            {
                var session = _wrapper._session;
                if (session != null)
                {
                    session.RaiseTerminal(reason);
                    return;
                }

                var userHandler = _wrapper._userHandler;
                if (userHandler != null)
                {
                    try
                    {
                        userHandler.OnStopped(reason);
                    }
                    catch (Exception ex)
                    {
                        _wrapper.Log.wtf(ex);
                    }
                }
            }
        }

        private sealed class ClientSession : IRawReliableClientHandler, IRawReliableEndpoint
        {
            private readonly RawReliableWrapperClient _wrapper;
            private readonly IRawReliableWrapperLogic _logic;
            private readonly IRawReliableClientHandler _userHandler;
            private readonly object _sendLock = new object();

            private volatile IRawReliableEndpoint? _endpoint;
            private NegotiationSession? _negotiation;

            private int _admitted;
            private int _disconnectedRaised;
            private int _terminalRaised;

            public ClientSession(RawReliableWrapperClient wrapper, IRawReliableWrapperLogic logic,
                IRawReliableClientHandler userHandler)
            {
                _wrapper = wrapper;
                _logic = logic;
                _userHandler = userHandler;
            }

            public void DisposeSession()
            {
                var negotiation = Interlocked.Exchange(ref _negotiation, null);
                negotiation?.Dispose();
            }

            public void RaiseDisconnected(StopReason reason)
            {
                if (Interlocked.Exchange(ref _disconnectedRaised, 1) != 0)
                {
                    return;
                }

                try
                {
                    _logic.OnDisconnected();
                }
                catch (Exception ex)
                {
                    _wrapper.Log.wtf(ex);
                }

                try
                {
                    _userHandler.OnDisconnected(reason);
                }
                catch (Exception ex)
                {
                    _wrapper.Log.wtf(ex);
                }
            }

            public void RaiseTerminal(StopReason reason)
            {
                if (Interlocked.Exchange(ref _terminalRaised, 1) != 0)
                {
                    return;
                }

                if (Volatile.Read(ref _admitted) != 0)
                {
                    RaiseDisconnected(reason);
                }

                try
                {
                    _userHandler.OnStopped(reason);
                }
                catch (Exception ex)
                {
                    _wrapper.Log.wtf(ex);
                }
            }

            void IRawReliableClientHandler.OnConnected(IRawReliableEndpoint endPoint)
            {
                _endpoint = endPoint;

                var negotiation = new NegotiationSession(
                    NegotiationRole.Client,
                    _wrapper._version,
                    _wrapper._negotiationName,
                    SendEnvelope,
                    _wrapper.StopFromSession,
                    _ => { },
                    HandleAccepted,
                    appendData: _logic.FillNegotiationData,
                    handshakeMaxByteSize: _wrapper._handshakeMaxByteSize,
                    rejectLinger: _wrapper._rejectLinger,
                    clock: _wrapper._clock,
                    log: _wrapper.Log);

                _negotiation = negotiation;
                negotiation.Start();
            }

            void IRawHandler.OnReceived(UnionDataList receivedBuffer)
            {
                var negotiation = _negotiation;
                if (Volatile.Read(ref _admitted) == 0)
                {
                    if (negotiation != null && negotiation.OnReceived(receivedBuffer))
                    {
                        return;
                    }

                    receivedBuffer.Release();
                    _wrapper.Stop(new ProtocolMismatch(_wrapper.Name,
                        "unexpected payload before the wrapper negotiation completed"));
                    return;
                }

                try
                {
                    if (_logic.ProcessReceivedData(receivedBuffer))
                    {
                        _userHandler.OnReceived(receivedBuffer.Acquire());
                        return;
                    }

                    _wrapper.Stop(new TextFail(_wrapper.Name,
                        "failed to process received wrapper data"));
                }
                catch (Exception ex)
                {
                    _wrapper.Stop(new ExceptionFail(_wrapper.Name, ex,
                        "failed to process received wrapper data"));
                }
                finally
                {
                    receivedBuffer.Release();
                }
            }

            void IRawReliableHandler.OnDisconnected(StopReason reason)
            {
                if (Volatile.Read(ref _admitted) != 0)
                {
                    RaiseDisconnected(reason);
                }
            }

            void IRawHandler.OnStopped(StopReason reason)
            {
                _wrapper.Stop(reason);
            }

            IEndPoint? IRawEndpoint.RemoteEndPoint => _endpoint?.RemoteEndPoint;

            bool IRawReliableEndpoint.IsConnected
            {
                get
                {
                    var endpoint = _endpoint;
                    return Volatile.Read(ref _admitted) != 0 && endpoint != null && endpoint.IsConnected;
                }
            }

            int IRawEndpoint.MessageMaxByteSize
            {
                get
                {
                    var endpoint = _endpoint;
                    return endpoint?.MessageMaxByteSize ?? _wrapper._inner.MessageMaxByteSize;
                }
            }

            SendResult IRawEndpoint.Send(UnionDataList bufferToSend)
            {
                if (bufferToSend == null)
                {
                    return SendResult.InvalidMessage;
                }

                if (Volatile.Read(ref _admitted) == 0)
                {
                    bufferToSend.Release();
                    return SendResult.NotConnected;
                }

                lock (_sendLock)
                {
                    try
                    {
                        var endpoint = _endpoint;
                        if (endpoint == null)
                        {
                            return SendResult.NotConnected;
                        }

                        if (!_logic.ProcessSentData(bufferToSend))
                        {
                            return SendResult.Error;
                        }

                        return endpoint.Send(bufferToSend.Acquire());
                    }
                    catch (Exception ex)
                    {
                        _wrapper.Log.wtf(ex);
                        return SendResult.Error;
                    }
                    finally
                    {
                        bufferToSend.Release();
                    }
                }
            }

            bool IRawReliableEndpoint.Disconnect(StopReason reason)
            {
                _wrapper.Stop(reason);
                return true;
            }

            void IBaseEndpoint.GetControls(List<IControl> dst, Predicate<IControl>? predicate)
            {
                _logic.GetControls(dst, predicate);
            }

            private void HandleAccepted(UnionDataList customTail)
            {
                try
                {
                    _logic.OnNegotiationAccepted(customTail);
                }
                catch (Exception ex)
                {
                    _wrapper.Stop(new ExceptionFail(_wrapper.Name, ex,
                        "wrapper negotiation acceptance hook failed"));
                    return;
                }

                Volatile.Write(ref _admitted, 1);

                try
                {
                    _logic.OnConnected();
                }
                catch (Exception ex)
                {
                    _wrapper.Stop(new ExceptionFail(_wrapper.Name, ex, "wrapper OnConnected failed"));
                    return;
                }

                try
                {
                    _userHandler.OnConnected(this);
                }
                catch (Exception ex)
                {
                    _wrapper.Stop(new ExceptionFail(_wrapper.Name, ex, "handler OnConnected failed"));
                }
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
                    _wrapper.Log.wtf(ex);
                    return SendResult.Error;
                }
            }
        }
    }
}
