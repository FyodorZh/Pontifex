using System;
using Actuarius.Memory;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Raw.Reliable.Ack.Protocols
{
    public class RawReliableAckWrapperClient<TLogic> : RawReliableAckWrapperClient
        where TLogic : IRawReliableAckWrapperClientLogic
    {
        public RawReliableAckWrapperClient(string typeName, IRawReliableAckClient transportToWrap, Func<ILogger, IMemoryRental, TLogic> constructor)
            : base(typeName, transportToWrap)
        {
            SetupLogic(constructor.Invoke(transportToWrap.Log, transportToWrap.Memory));
        }
    }

    public class RawReliableAckWrapperClient : RawReliableAckClient, IRawReliableAckClientHandler
    {
        private readonly IRawReliableAckClient mBaseTransport;
        private IRawReliableAckWrapperClientLogic? mLogic;

        private bool mInConnectionProcess;

        private ClientHandler? mClientHandler;
        
        public override int MessageMaxByteSize => mBaseTransport.MessageMaxByteSize;

        public RawReliableAckWrapperClient(string typeName, IRawReliableAckClient transportToWrap)
            : base(typeName, transportToWrap.Log, transportToWrap.Memory)
        {
            mBaseTransport = transportToWrap;
        }

        protected void SetupLogic(IRawReliableAckWrapperClientLogic logic)
        {
            mLogic = logic;
        }

        protected override IRawReliableAckClientHandler? SetupHandler(IRawReliableAckClientHandler handler)
        {
            var logic = mLogic;
            if (logic != null)
            {
                mClientHandler = new ClientHandler(this, logic, handler);
                mBaseTransport.Init(mClientHandler);
                return this;
            }

            return null;
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
                    Stop(new StopReasons.ChainFail(Name, r, "Unexpected underlying transport stop"));
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

        internal void ConnectionFinished_Internal(IRawReliableEndpoint endPoint, UnionDataList ackResponse)
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
            return $"{Name}<{coreName}>";
        }

        #region IRawAckClientHandler (for internal usage)

        void IRawReliableAckClientHandler.FillAckData(UnionDataList ackData)
        {
            ackData.Release();
            Fail("WriteAckData", "this method must not be called");
        }

        void IRawReliableHandler.OnDisconnected(StopReason reason)
        {
            IRawReliableEndpoint? ep = mClientHandler;
            ep?.Disconnect(reason);
        }

        void IRawHandler.OnReceived(UnionDataList receivedBuffer)
        {
            receivedBuffer.Release();
            Fail("OnReceived", "this method must not be called");
        }

        void IRawReliableAckClientHandler.OnConnected(IRawReliableEndpoint endPoint, UnionDataList ackResponse)
        {
            // DO NOTHING
        }

        void IRawReliableClientHandler.OnStopped(StopReason reason)
        {
            // DO NOTHING
        }

        #endregion
    }
}