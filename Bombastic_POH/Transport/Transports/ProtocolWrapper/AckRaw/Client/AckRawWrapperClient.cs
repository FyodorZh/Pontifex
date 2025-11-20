using Shared;
using Transport.Abstractions.Clients;
using Transport.Abstractions.Endpoints.Client;
using Transport.Abstractions.Handlers;
using Transport.Abstractions.Handlers.Client;
using Shared.Buffer;
using Transport.Transports.Core;

namespace Transport.Transports.ProtocolWrapper.AckRaw
{
    public class AckRawWrapperClient<Logic> : AckRawWrapperClient
        where Logic : IAckRawWrapperClientLogic
    {
        public AckRawWrapperClient(string typeName, IAckRawClient transportToWrap, IConstructor<Logic> constructor)
            : base(typeName, transportToWrap)
        {
            SetupLogic(constructor.Construct());
        }
    }

    public class AckRawWrapperClient : AckRawClient, IAckRawClientHandler
    {
        private readonly IAckRawClient mBaseTransport;
        private IAckRawWrapperClientLogic mLogic;

        private bool mInConnectionProcess;

        private ClientHandler mClientHandler;

        public AckRawWrapperClient(string typeName, IAckRawClient transportToWrap)
            : base(typeName)
        {
            mBaseTransport = transportToWrap;
            AppendControl(transportToWrap);
        }

        public override int MessageMaxByteSize
        {
            get
            {
                return mBaseTransport.MessageMaxByteSize;
            }
        }

        protected void SetupLogic(IAckRawWrapperClientLogic logic)
        {
            mLogic = logic;
            AppendControl(logic.Controls);
        }

        protected override IAckRawClientHandler SetupHandler(IAckRawClientHandler handler)
        {
            mClientHandler = new ClientHandler(this, mLogic, handler);
            mBaseTransport.Init(mClientHandler);
            return this;
        }

        protected override bool BeginConnect()
        {
            mInConnectionProcess = true;
            return mBaseTransport.Start(r =>
            {
                if (mInConnectionProcess)
                {
                    mInConnectionProcess = false;
                    ConnectionFailed();
                }
                if (IsStarted)
                {
                    Stop(new StopReasons.ChainFail(Type, r, "Unexpected underlying transport stop"));
                }
            }, Log);
        }

        protected override void OnReadyToConnect()
        {
            // TODO: fix race
        }

        protected override void DestroyTransport(StopReason reason)
        {
            mBaseTransport.Stop();
        }

        internal void ConnectionFinished_Internal(IAckRawServerEndpoint endPoint, ByteArraySegment ackResponse)
        {
            if (mInConnectionProcess)
            {
                mInConnectionProcess = false;
                ConnectionFinished(endPoint, ackResponse);
            }
        }

        public override string ToString()
        {
            string coreName = mBaseTransport != null ? mBaseTransport.ToString() : "null-core";
            return string.Format("{0}<{1}>", Type, coreName);
        }

        #region IAckRawClientHandler (for internal usage)

        void IAckHandler.WriteAckData(UnionDataList ackData)
        {
            Fail("GetAckData", "this method must not be called");
        }

        void IRawBaseHandler.OnDisconnected(StopReason reason)
        {
            IAckRawServerEndpoint ep = mClientHandler;
            if (ep != null)
            {
                ep.Disconnect(reason);
            }
        }

        void IRawBaseHandler.OnReceived(IMemoryBufferHolder receivedBuffer)
        {
            receivedBuffer.Release();
            Fail("OnReceived", "this method must not be called");
        }

        void IAckRawClientHandler.OnConnected(IAckRawServerEndpoint endPoint, ByteArraySegment ackResponse)
        {
            // DO NOTHING
        }

        void IAckRawClientHandler.OnStopped(StopReason reason)
        {
            // DO NOTHING
        }

        #endregion
    }
}