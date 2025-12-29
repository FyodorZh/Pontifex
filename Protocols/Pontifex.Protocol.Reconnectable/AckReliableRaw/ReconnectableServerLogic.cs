using System;
using Actuarius.Collections;
using Actuarius.Memory;
using Pontifex.Abstractions.Endpoints.Server;
using Pontifex.Abstractions.Handlers.Server;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Protocols.Reconnectable.AckReliableRaw
{
    class ReconnectableServerLogic : ReconnectableBaseLogic<IAckRawClientEndpoint>, IAckRawServerHandler, IAckRawClientEndpoint
    {
        private IMultiRefReadOnlyByteArray? _secret;

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

        public bool Attach(SessionId sessionId)
        {
            if (sessionId.IsValid)
            {
                _sessionId = sessionId;
                _secret = new StaticReadOnlyByteArray(Guid.NewGuid().ToByteArray());
                mAttached = true;
                return true;
            }
            return false;
        }

        public bool Reattach(IMultiRefReadOnlyByteArray secret)
        {
            using var secretDisposer = secret.AsDisposable();
            if (mAttached)
            {
                Log.w("{sessionId}: Reattach failed due to true multiple reconnection", this);
                return false;
            }

            if (!_secret.EqualByContent(secret))
            {
                Log.w("{sessionId}: Reattach failed due to secret difference. '{secret}' vs '{newSecret}'", this, _secret?.ToString() ?? "null", secret);
                return false;
            }

            mAttached = true;
            return true;
        }

        void IAckRawServerHandler.GetAckResponse(UnionDataList ackData)
        {
            mUserHandler.GetAckResponse(ackData);
            ackData.PutFirst(new UnionData(_secret));
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