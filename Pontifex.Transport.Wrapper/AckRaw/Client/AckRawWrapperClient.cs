using System;
using Actuarius.Memory;
using Pontifex.Utils;
using Scriba;
using Transport.Abstractions.Clients;
using Transport.Abstractions.Endpoints.Client;
using Transport.Abstractions.Handlers;
using Transport.Abstractions.Handlers.Client;
using Transport.Transports.Core;

namespace Transport.Transports.ProtocolWrapper.AckRaw
{
    public class AckRawWrapperClient<TLogic> : AckRawWrapperClient
        where TLogic : IAckRawWrapperClientLogic
    {
        public AckRawWrapperClient(string typeName, IAckRawClient transportToWrap, Func<ILogger, IMemoryRental, TLogic> constructor)
            : base(typeName, transportToWrap)
        {
            SetupLogic(constructor.Invoke(transportToWrap.Log, transportToWrap.Memory));
        }
    }

    public class AckRawWrapperClient : AckRawClient, IAckRawClientHandler
    {
        private readonly IAckRawClient mBaseTransport;
        private IAckRawWrapperClientLogic mLogic = null!;

        private bool mInConnectionProcess;

        private ClientHandler? mClientHandler;
        
        public override int MessageMaxByteSize => mBaseTransport.MessageMaxByteSize;

        public AckRawWrapperClient(string typeName, IAckRawClient transportToWrap)
            : base(typeName, transportToWrap.Log, transportToWrap.Memory)
        {
            mBaseTransport = transportToWrap;
            AppendControl(transportToWrap);
        }

        protected void SetupLogic(IAckRawWrapperClientLogic logic)
        {
            mLogic = logic;
            if (logic.Controls != null)
            {
                AppendControl(logic.Controls);
            }
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
            });
        }

        protected override void OnReadyToConnect()
        {
            // TODO: fix race
        }

        protected override void DestroyTransport(StopReason reason)
        {
            mBaseTransport.Stop();
        }

        internal void ConnectionFinished_Internal(IAckRawServerEndpoint endPoint, UnionDataList ackResponse)
        {
            if (mInConnectionProcess)
            {
                mInConnectionProcess = false;
                ConnectionFinished(endPoint, ackResponse);
            }
        }

        public override string ToString()
        {
            string coreName = mBaseTransport.ToString();
            return $"{Type}<{coreName}>";
        }

        public void Setup(IMemoryRental memory, ILogger logger)
        {
            mClientHandler!.Setup(memory, logger);
        }

        #region IAckRawClientHandler (for internal usage)

        void IAckHandler.WriteAckData(UnionDataList ackData)
        {
            ackData.Release();
            Fail("WriteAckData", "this method must not be called");
        }

        void IRawBaseHandler.OnDisconnected(StopReason reason)
        {
            IAckRawServerEndpoint? ep = mClientHandler;
            ep?.Disconnect(reason);
        }

        void IRawBaseHandler.OnReceived(UnionDataList receivedBuffer)
        {
            receivedBuffer.Release();
            Fail("OnReceived", "this method must not be called");
        }

        void IAckRawClientHandler.OnConnected(IAckRawServerEndpoint endPoint, UnionDataList ackResponse)
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