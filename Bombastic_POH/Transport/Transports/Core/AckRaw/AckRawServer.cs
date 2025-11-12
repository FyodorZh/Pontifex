using System;
using Shared;
using Transport.Abstractions.Acknowledgers;
using Transport.Abstractions.Handlers.Server;
using Transport.Abstractions.Servers;

namespace Transport.Transports.Core
{
    public abstract class AckRawServer : AbstractTransport, IAckRawServer
    {
        private bool mIsInitialized;

        private readonly string mTypeName;

        private IRawServerAcknowledger<IAckRawServerHandler> mAcknowledger;

        protected AckRawServer(string typeName)
        {
            mTypeName = typeName;
        }

        protected virtual IRawServerAcknowledger<IAckRawServerHandler> SetupAcknowledger(IRawServerAcknowledger<IAckRawServerHandler> acknowledger)
        {
            return acknowledger;
        }

        public sealed override string Type
        {
            get { return mTypeName; }
        }

        public bool IsInitialized
        {
            get
            {
                lock (mLocker)
                {
                    return mIsInitialized;
                }
            }
        }

        public bool Init(IRawServerAcknowledger<IAckRawServerHandler> acknowledger)
        {
            lock (mLocker)
            {
                if (IsValid)
                {
                    if (!IsStarted && !mIsInitialized)
                    {
                        if (acknowledger != null)
                        {
                            var processedAcknowledger = SetupAcknowledger(acknowledger);
                            if (processedAcknowledger != null)
                            {
                                mAcknowledger = processedAcknowledger;
                                mIsInitialized = true;
                                return true;
                            }
                            Fail("Init", "SetupAcknowledger() returned null");
                        }
                        else
                        {
                            Fail("Init", "acknowledger is null");
                        }
                    }
                    else
                    {
                        Fail("Init", "Wrong transport state (initialized={0}, started={1})", mIsInitialized, IsStarted);
                    }
                }
                return false;
            }
        }

        protected sealed override void OnStarted()
        {
            // DO NOTHING
        }

        public abstract int MessageMaxByteSize { get; }

        protected IAckRawServerHandler TryConnectNewClient(ByteArraySegment ackData)
        {
            if (!IsValid)
            {
                Fail("TryConnectNewClient", "Transport is not valid");
                return null;
            }

            if (!IsStarted || !mIsInitialized)
            {
                Fail("TryConnectNewClient", "Wrong transport state (initialized={0}, started={1})", mIsInitialized, IsStarted);
                return null;
            }

            var acknowledger = mAcknowledger;
            if (acknowledger != null)
            {
                var handler = acknowledger.TryAck(ackData, Log);
                if (handler != null)
                {
                    handler = handler.Test(text => Log.e(text)).GetSafe(e => Log.e(e.ToString()));
                }
                return handler;
            }
            return null;
        }

    }
}
