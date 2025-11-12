using System;
using Shared;
using Transport.Serializer;
using Transport.Abstractions.Clients;
using Transport.Abstractions.Endpoints.Client;
using Transport.Abstractions.Handlers;
using Transport.Abstractions.Handlers.Client;
using Shared.Buffer;

namespace Transport.Protocols
{
    public enum CSPState
    {
        Constructed,
        Connecting,
        Acknowledged,
        Active,
        Destroyed
    }

    public abstract class BaseAckCSP<TSerializer>: IAckRawClientHandler
    {
        private readonly IAckRawClient mClient;
        private readonly byte[] mAckData;
        private readonly ITransportSerializer<TSerializer> mSerializer;
        
        private CSPState mState = CSPState.Constructed;
        private volatile IAckRawServerEndpoint mEndpoint;

        protected BaseAckCSP(IAckRawClient client, byte[] ackData, ITransportSerializer<TSerializer> serializer)
        {
            mClient = client;
            mAckData = ackData;
            mSerializer = serializer;
        }

        #region Implementation of IAckClientHandler

        byte[] IAckHandler.GetAckData()
        {
            return mAckData;
        }

        #endregion

        #region Implementation of IAckRawClientHandler

        void IAckRawClientHandler.OnConnected(IAckRawServerEndpoint endPoint, ByteArraySegment ackResponse)
        {
            mEndpoint = endPoint;
            ProtocolState = CSPState.Acknowledged;
            OnAcknowledged();
        }

        void IRawBaseHandler.OnDisconnected(StopReason stopReason)
        {
            mEndpoint = null;
            ProtocolState = CSPState.Destroyed;
            
            OnDisconnected(DisconnectReason.Disconnected);
        }

        void IAckRawClientHandler.OnStopped(StopReason stopReason)
        {
            ProtocolState = CSPState.Destroyed;
        }

        void IRawBaseHandler.OnReceived(IMemoryBufferHolder receivedBuffer)
        {
            using (var bufferAccessor = receivedBuffer.ExposeAccessorOnce())
            {
                ByteArraySegment data;
                if (!bufferAccessor.Buffer.PopFirst().AsArray(out data))
                {
                    Log.e("Failed to parse incoming message");
                    mClient.Stop();
                    return;
                }

                TSerializer deserializedData = mSerializer.Deserialize(data);
                OnReceived(deserializedData);
            }
        }

        #endregion

        public virtual bool InitClient(IAckRawClient client)
        {
            return client.Init(this);
        }

        public bool Connect(Action<StopReason> onStopped = null)
        {
            bool result = InitClient(mClient) && mClient.Start(onStopped, Log.StaticLogger);
            if (result)
            {
                ProtocolState = CSPState.Connecting;
            }
            else
            {
                ProtocolState = CSPState.Destroyed;
            }

            return result;
        }

        public CSPState ProtocolState
        {
            get { return mState; }
            protected set
            {
                if (mState < value)
                {
                    mState = value;
                }
                else if (mState > value)
                {
                    Log.e("Wrong state usage");
                }
            }
        }

        public virtual void Destroy()
        {
            mEndpoint = null;
            mClient.Stop();
        }
        
        protected bool SendData(TSerializer data)
        {
            byte[] bytes = mSerializer.Serialize(data);
            IAckRawServerEndpoint endPoint = mEndpoint;
            if (endPoint != null && endPoint.IsConnected)
            {
                var buffer = ConcurrentUsageMemoryBufferPool.Instance.AllocateAndPush(bytes);
                return endPoint.Send(buffer) == SendResult.Ok;
            }
            return false;
        }

        protected bool IsConnected
        {
            get
            {
                IAckRawServerEndpoint endPoint = mEndpoint;
                return endPoint != null && endPoint.IsConnected;
            }
        }

        protected abstract void OnAcknowledged();
        protected abstract void OnDisconnected(DisconnectReason reason);
        protected abstract void OnReceived(TSerializer data);
    }
}