using System;
using Actuarius.Memory;
using Pontifex.Utils;
using Transport.Abstractions.Acknowledgers;
using Transport.Abstractions.Handlers.Server;

namespace Transport.Transports.ProtocolWrapper.AckRaw
{
    public class AcknowledgerWrapper<THandlerWrapper> : AcknowledgerWrapper
        where THandlerWrapper : IHandlerWrapper
    {
        private readonly Func<THandlerWrapper> _ctor;

        public AcknowledgerWrapper(Func<THandlerWrapper> constructor)
        {
            _ctor = constructor;
        }

        protected override IHandlerWrapper ConstructWrapper()
        {
            return _ctor.Invoke();
        }
    }

    public abstract class AcknowledgerWrapper : IRawServerAcknowledger<IAckRawServerHandler>
    {
        private Action<string> _onFail = null!;
        private IRawServerAcknowledger<IAckRawServerHandler> _wrappedAcknowledger = null!;

        public void Init(IRawServerAcknowledger<IAckRawServerHandler> wrappedAcknowledger, Action<string> onFail)
        {
            _onFail = onFail;
            _wrappedAcknowledger = wrappedAcknowledger;
        }

        public void Setup(IMemoryRental memory, ILogger logger)
        {
            _wrappedAcknowledger.Setup(memory, logger);
        }

        public IAckRawServerHandler? TryAck(UnionDataList ackData)
        {
            var wrapper = ConstructWrapper();
            bool isOK = wrapper.CheckAckData(ackData);
            if (isOK)
            {
                IAckRawServerHandler? coreHandler = _wrappedAcknowledger.TryAck(ackData);
                if (coreHandler != null)
                {
                    wrapper.Init(coreHandler.Test(_onFail).GetSafe(e => _onFail(e.ToString())));
                    return wrapper;
                }
            }
            return null;
        }

        protected abstract IHandlerWrapper ConstructWrapper();
    }
}
