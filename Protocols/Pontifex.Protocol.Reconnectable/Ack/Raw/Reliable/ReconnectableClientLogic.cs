using System;
using Actuarius.Collections;
using Actuarius.Memory;
using Operarius;
using Pontifex.Ack;
using Pontifex.Ack.Raw.Reliable;
using Pontifex.StopReasons;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Ack.Raw.Reliable.Reconnectable
{
    class ReconnectableClientLogic : ReconnectableBaseLogic<IAckRawReliableClientSideEndpoint>, IAckRawReliableClientHandler, IAckRawReliableClientSideEndpoint
    {
        private readonly Func<IAckRawReliableClient?> _underlyingTransportFactory;
        private readonly IAckRawReliableClientHandler _userHandler;

        private readonly ThreadSafeDateTime _nextReconnectionTime = new ThreadSafeDateTime();

        private IMultiRefReadOnlyByteArray? _secret;

        public event Action<IAckRawReliableClientSideEndpoint, UnionDataList>? OnConnected;

        public SessionId SessionId => _sessionId;

        public ReconnectableClientLogic(Func<IAckRawReliableClient?> underlyingTransportFactory, IAckRawReliableClientHandler userHandler, TimeSpan disconnectTimeout, 
            ILogger logger, IMemoryRental memoryRental)
            : base(userHandler, disconnectTimeout, logger, memoryRental)
        {
            _underlyingTransportFactory = underlyingTransportFactory;
            _userHandler = userHandler;
            

            _nextReconnectionTime.Time = DateTime.UtcNow;
        }

        protected override bool BeginReconnect()
        {
            if (_nextReconnectionTime.Time > DateTime.UtcNow)
            {
                return false;
            }

            IAckRawReliableClient? transport = _underlyingTransportFactory.Invoke();
            if (transport == null)
            {
                return false;
            }

            if (!transport.Init(this))
            {
                return false;
            }

            _nextReconnectionTime.Time = DateTime.UtcNow.AddSeconds(ReconnectableInfo.ReconnectionPeriod.Seconds);
            return transport.Start(r =>
            {
                Log.i($"TODO??: I need to react on this STOP: {r}");
            });
        }
        
        public override void OnDisconnected(StopReason reason)
        {
            _nextReconnectionTime.Time = DateTime.UtcNow.AddSeconds(ReconnectableInfo.ReconnectionPeriod.Seconds);
        }

        #region IAckRawClientHandler

        void IAckRawClientHandler.FillAckData(UnionDataList ackData)
        {
            _userHandler.FillAckData(ackData);
            if (_sessionId.IsValid)
            {
                ackData.PutFirst(_secret ?? throw new InvalidOperationException("Secret must be set before sending ack data"));
            }
            ackData.PutFirst(_sessionId.Generation);
            ackData.PutFirst(_sessionId.Id);
            ackData.PutFirst(ReconnectableInfo.AckRequest);
        }

        void IAckRawReliableClientHandler.OnConnected(IAckRawReliableClientSideEndpoint endPoint, UnionDataList ackResponse)
        {
            using var ackResponseDisposer = ackResponse.AsDisposable();
            if (!ackResponse.TryPopFirst(out IMultiRefReadOnlyByteArray? ackBytes))
            {
                Fail("Disconnecting due to wrong transport ack response format");
                return;
            }

            using var ackBytesDisposer = ackBytes.AsDisposable();
            if (!ackBytes.EqualByContent(ReconnectableInfo.AckOKResponse))
            {
                Fail("Disconnecting due to wrong transport ack response (1)");
                return;
            }
            
            if (!ackResponse.TryPopFirst(out int id) || !ackResponse.TryPopFirst(out int generation))
            {
                Fail("Disconnecting due to wrong transport ack response (2)");
                return;
            }

            if (!ackResponse.TryPopFirst(out _secret))
            {
                Fail("Disconnecting due to wrong transport ack response (3)");
                return;
            }

            SessionId sessionId = new SessionId(id, generation);
            if (sessionId.IsValid)
            {
                if (!_sessionId.IsValid || _sessionId.Equals(sessionId))
                {
                    _sessionId = sessionId;

                    Connect(endPoint, out var isFirstConnection);

                    if (isFirstConnection)
                    {
                        Log.i("Logic connected");
                        OnConnected?.Invoke(this, ackResponse.Acquire());
                    }

                    return;
                }

                Fail($"Disconnecting due to session id mismatch. Expected {_sessionId}, received {sessionId}");
            }
            else
            {
                Fail("Disconnecting due to incorrect session id");
            }
        }

        void IAckRawClientHandler.OnStopped(StopReason reason)
        {
            if (reason is Induced { Cause: AckRejected })
            {
                Stop(reason);
            }
            else
            {
                OnConnectionStopped(reason);
            }
        }

        #endregion IAckRawClientHandler

        public override string ToString()
        {
            return "";
        }
    }
}