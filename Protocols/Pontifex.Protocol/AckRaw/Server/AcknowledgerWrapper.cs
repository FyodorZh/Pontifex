using System;
using Pontifex.Ack.Raw;
using Pontifex.Ack.Raw.Reliable;
using Pontifex.Utils;

namespace Pontifex.Protocols
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

    public abstract class AcknowledgerWrapper : IRawServerAcknowledger<IAckRawReliableServerHandler>
    {
        private Action<string> _onFail = null!;
        private IRawServerAcknowledger<IAckRawReliableServerHandler> _wrappedAcknowledger = null!;

        public void Init(IRawServerAcknowledger<IAckRawReliableServerHandler> wrappedAcknowledger, Action<string> onFail)
        {
            _onFail = onFail;
            _wrappedAcknowledger = wrappedAcknowledger;
        }

        public IAckRawReliableServerHandler? TryAck(UnionDataList ackData)
        {
            var wrapper = ConstructWrapper();
            bool isOK = wrapper.CheckAckData(ackData);
            if (isOK)
            {
                IAckRawReliableServerHandler? coreHandler = _wrappedAcknowledger.TryAck(ackData);
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
