using System;
using Pontifex.Utils;
using Shared;
using Transport.Abstractions.Endpoints.Server;
using Transport.Abstractions.Handlers.Server;
using TimeSpan = System.TimeSpan;

namespace Transport.Protocols.Reconnectable.AckReliableRaw
{
    class ReconnectableServerLogic : ReconnectableBaseLogic<IAckRawClientEndpoint>, IAckRawServerHandler, IAckRawClientEndpoint
    {
        private UnionDataList mAckData;

        private readonly IAckRawServerHandler mUserHandler;

        private volatile bool mAttached = false;

        public event Action<IAckRawClientEndpoint> OnConnected;

        public ReconnectableServerLogic(IAckRawServerHandler userHandler, TimeSpan disconnectTimeout)
            : base(userHandler, disconnectTimeout)
        {
            mUserHandler = userHandler;
        }

        protected override bool BeginReconnect()
        {
            return false; // ждём клиента, не делаем ничего
        }

        public override void OnDisconnected(StopReason reason)
        {
            OnConnectionStopped(reason);
            mAttached = false;
        }

        public bool Attach(SessionId sessionId, UnionDataList ackData)
        {
            if (mUserHandler != null && sessionId.IsValid)
            {
                mSessionId = sessionId;
                mAckData = ackData.Clone();
                mAttached = true;
                return true;
            }
            return false;
        }

        public bool Reattach(UnionDataList ackData)
        {
            if (mAttached)
            {
                Log.w("{sessionId}: Reattach failed due to true multiple reconnection", this);
                return false;
            }

            if (!mAckData.EqualByContent(ackData))
            {
                Log.w("{sessionId}: Reattach failed due to ack-data difference. '{oldAck}' vs '{newAck}'", this, mAckData, ackData);
                return false;
            }

            mAttached = true;
            return true;
        }

        byte[] IAckRawServerHandler.GetAckResponse()
        {
            byte[] ackResponse = AckUtils.AppendPrefix(mUserHandler.GetAckResponse(), mSessionId.Serialize());
            ackResponse = AckUtils.AppendPrefix(ackResponse, ReconnectableInfo.AckOKResponse);
            return ackResponse;
        }

        void IAckRawServerHandler.OnConnected(IAckRawClientEndpoint endPoint)
        {
            bool isFirstConnection;
            Connect(endPoint, out isFirstConnection);
            if (isFirstConnection)
            {
                var onConnectd = OnConnected;
                if (onConnectd != null)
                {
                    onConnectd(this);
                }
            }
        }

        public override string ToString()
        {
            return "Session[" + (mSessionId.IsValid ? mSessionId.ToString() : "invalid-session") + "]";
        }
    }
}