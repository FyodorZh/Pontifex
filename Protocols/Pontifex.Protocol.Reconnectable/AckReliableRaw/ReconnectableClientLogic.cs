using System;
using Actuarius.Collections;
using Actuarius.Memory;
using Actuarius.PeriodicLogic;
using Pontifex;
using Pontifex.Abstractions.Clients;
using Pontifex.Abstractions.Endpoints.Client;
using Pontifex.Abstractions.Handlers;
using Pontifex.Abstractions.Handlers.Client;
using Pontifex.StopReasons;
using Pontifex.Utils;

namespace Transport.Protocols.Reconnectable.AckReliableRaw
{
    class ReconnectableClientLogic : ReconnectableBaseLogic<IAckRawServerEndpoint>, IAckRawClientHandler, IAckRawServerEndpoint
    {
        private readonly Func<IAckReliableRawClient> mTransportFactory;
        private readonly IAckRawClientHandler mUserHandler;

        private readonly ThreadSafeDateTime mNextReconnectionTime = new ThreadSafeDateTime();

        public event Action<IAckRawServerEndpoint, UnionDataList>? OnConnected;

        public SessionId SessionId => mSessionId;

        public ReconnectableClientLogic(Func<IAckReliableRawClient> transportFactory, IAckRawClientHandler userHandler, TimeSpan disconnectTimeout)
            : base(userHandler, disconnectTimeout)
        {
            mUserHandler = userHandler;
            mTransportFactory = transportFactory;

            mNextReconnectionTime.Time = DateTime.UtcNow;
        }

        protected override bool BeginReconnect()
        {
            if (mNextReconnectionTime.Time > DateTime.UtcNow)
            {
                return false;
            }

            IAckReliableRawClient transport = mTransportFactory.Invoke();
            if (transport == null)
            {
                return false;
            }

            if (!transport.Init(this))
            {
                return false;
            }

            mNextReconnectionTime.Time = DateTime.UtcNow.AddSeconds(ReconnectableInfo.ReconnectionPeriod.Seconds);
            return transport.Start(r => { });
        }

        #region IAckRawClientHandler

        void IAckHandler.WriteAckData(UnionDataList ackData)
        {
            mUserHandler.WriteAckData(ackData);
            ackData.PutFirst(mSessionId.Generation);
            ackData.PutFirst(mSessionId.Id);
            ackData.PutFirst(ReconnectableInfo.AckRequest);
        }

        void IAckRawClientHandler.OnConnected(IAckRawServerEndpoint endPoint, UnionDataList ackResponse)
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

            SessionId sessionId = new SessionId(id, generation);
            if (sessionId.IsValid)
            {
                if (!mSessionId.IsValid || mSessionId.Equals(sessionId))
                {
                    mSessionId = sessionId;

                    Connect(endPoint, out var isFirstConnection);

                    if (isFirstConnection)
                    {
                        Log.i("Logic connected");
                        OnConnected?.Invoke(this, ackResponse);
                    }

                    return;
                }

                Fail($"Disconnecting due to session id mismatch. Expected {mSessionId}, received {sessionId}");
            }
            else
            {
                Fail("Disconnecting due to incorrect session id");
            }
        }

        public override void OnDisconnected(StopReason reason)
        {
            mNextReconnectionTime.Time = DateTime.UtcNow.AddSeconds(ReconnectableInfo.ReconnectionPeriod.Seconds);
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