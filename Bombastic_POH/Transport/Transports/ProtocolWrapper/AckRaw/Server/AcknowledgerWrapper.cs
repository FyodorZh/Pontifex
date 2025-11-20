using System;
using Shared;
using Transport.Abstractions.Acknowledgers;
using Transport.Abstractions.Handlers.Server;

namespace Transport.Transports.ProtocolWrapper.AckRaw
{
    public class AcknowledgerWrapper<THandlerWrapper> : AcknowledgerWrapper
        where THandlerWrapper : IHandlerWrapper
    {
        private readonly IConstructor<THandlerWrapper> mCtor;

        public AcknowledgerWrapper(IConstructor<THandlerWrapper> constructor)
        {
            mCtor = constructor;
        }

        protected override IHandlerWrapper ConstructWrapper()
        {
            return mCtor.Construct();
        }
    }

    public abstract class AcknowledgerWrapper : IRawServerAcknowledger<IAckRawServerHandler>
    {
        private Action<string> mOnFail;
        private volatile IRawServerAcknowledger<IAckRawServerHandler> mWrappedAcknowledger;

        public void Init(IRawServerAcknowledger<IAckRawServerHandler> wrappedAcknowledger, Action<string> onFail)
        {
            mOnFail = onFail;
            mWrappedAcknowledger = wrappedAcknowledger;
        }

        public IAckRawServerHandler TryAck(UnionDataList ackData, ILogger logger)
        {
            var wrapper = ConstructWrapper();
            bool isOK = wrapper.CheckAckData(ackData);
            if (isOK)
            {
                IAckRawServerHandler coreHandler = mWrappedAcknowledger.TryAck(ackData, logger);
                if (coreHandler != null)
                {
                    wrapper.Init(coreHandler.Test(mOnFail).GetSafe(e => mOnFail(e.ToString())));
                    return wrapper;
                }
            }
            return null;
        }

        protected abstract IHandlerWrapper ConstructWrapper();
    }
}
