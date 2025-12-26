using System;
using Actuarius.Memory;
using Pontifex;
using Pontifex.Abstractions.Endpoints.Server;
using Pontifex.Abstractions.Handlers.Server;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Protocols.Reconnectable.AckReliableRaw
{
    class ReconnectableServerLogic : ReconnectableBaseLogic<IAckRawClientEndpoint>, IAckRawServerHandler, IAckRawClientEndpoint
    {
        private UnionDataList? mAckData;

        private readonly IAckRawServerHandler mUserHandler;

        private volatile bool mAttached;

        public event Action<IAckRawClientEndpoint>? OnConnected;

        public ReconnectableServerLogic(IAckRawServerHandler userHandler, TimeSpan disconnectTimeout, ILogger logger, IMemoryRental memoryRental)
            : base(userHandler, disconnectTimeout, logger, memoryRental)
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
            if (sessionId.IsValid)
            {
                _sessionId = sessionId;
                mAckData = ackData;
                mAttached = true;
                return true;
            }
            ackData.Release();
            return false;
        }

        public bool Reattach(UnionDataList ackData)
        {
            using var ackDataDisposer = ackData.AsDisposable();
            if (mAttached)
            {
                Log.w("{sessionId}: Reattach failed due to true multiple reconnection", this);
                return false;
            }

            if (mAckData == null)
            {
                Log.e("{sessionId}: Reattach failed due to null ack-data (ref)", this);
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

        void IAckRawServerHandler.GetAckResponse(UnionDataList ackData)
        {
            mUserHandler.GetAckResponse(ackData);
            ackData.PutFirst(new UnionData(_sessionId.Generation));
            ackData.PutFirst(new UnionData(_sessionId.Id));
            ackData.PutFirst(new UnionData(ReconnectableInfo.AckOKResponse));
        }

        void IAckRawServerHandler.OnConnected(IAckRawClientEndpoint endPoint)
        {
            Connect(endPoint, out var isFirstConnection);
            if (isFirstConnection)
            {
                OnConnected?.Invoke(this);
            }
        }

        public override string ToString()
        {
            return "Session[" + (_sessionId.IsValid ? _sessionId.ToString() : "invalid-session") + "]";
        }
    }
}