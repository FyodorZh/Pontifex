using System;
using Shared;
using Transport.Abstractions.Clients;
using Transport.Abstractions.Endpoints.Client;
using Transport.Abstractions.Handlers;
using Transport.Abstractions.Handlers.Client;
using TimeSpan = System.TimeSpan;

namespace Transport.Protocols.Reconnectable.AckReliableRaw
{
    class ReconnectableClientLogic : ReconnectableBaseLogic<IAckRawServerEndpoint>, IAckRawClientHandler, IAckRawServerEndpoint
    {
        private readonly Func<IAckReliableRawClient> mTransportFactory;
        private readonly IAckRawClientHandler mUserHandler;

        private readonly ThreadSafeDateTime mNextReconnectionTime = new ThreadSafeDateTime();

        public event Action<IAckRawServerEndpoint, ByteArraySegment> OnConnected;

        public SessionId SessionId
        {
            get { return mSessionId; }
        }

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
            return transport.Start(r => { }, Log);
        }

        #region IAckRawClientHandler

        byte[] IAckHandler.GetAckData()
        {
            byte[] ackData = AckUtils.AppendPrefix(mUserHandler.GetAckData(), mSessionId.Serialize());
            return AckUtils.AppendPrefix(ackData, ReconnectableInfo.AckRequest);
        }

        void IAckRawClientHandler.OnConnected(IAckRawServerEndpoint endPoint, ByteArraySegment ackResponse)
        {
            ackResponse = AckUtils.CheckPrefix(ackResponse, ReconnectableInfo.AckOKResponse);
            if (ackResponse.IsValid && ackResponse.Count > 1)
            {
                ByteArraySegment sessionIdBytes;
                ackResponse = AckUtils.GetPrefix(ackResponse, ackResponse[0], out sessionIdBytes);

                SessionId sessionId;
                if (sessionIdBytes.IsValid && SessionId.TryDeserialize(out sessionId, sessionIdBytes) && sessionId.IsValid)
                {
                    if (!mSessionId.IsValid || mSessionId.Equals(sessionId))
                    {
                        mSessionId = sessionId;

                        bool isFirstConnection;
                        Connect(endPoint, out isFirstConnection);

                        if (isFirstConnection)
                        {
                            Log.i("Logic connected");
                            var onConnected = OnConnected;
                            if (onConnected != null)
                            {
                                onConnected(this, ackResponse);
                            }
                        }

                        return;
                    }
                    Fail(string.Format("Disconnecting due to session id mismatch. Expected {0}, received {1}", mSessionId, sessionId));
                }
                else
                {
                    Fail("Disconnecting due to incorrect session id");
                }
            }
            else
            {
                Fail("Disconnecting due to wrong transport ack response");
            }
        }

        public override void OnDisconnected(StopReason reason)
        {
            mNextReconnectionTime.Time = DateTime.UtcNow.AddSeconds(ReconnectableInfo.ReconnectionPeriod.Seconds);
        }

        void IAckRawClientHandler.OnStopped(StopReason reason)
        {
            StopReasons.Induced r = reason as StopReasons.Induced;
            if (r != null && r.Cause is StopReasons.AckRejected)
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