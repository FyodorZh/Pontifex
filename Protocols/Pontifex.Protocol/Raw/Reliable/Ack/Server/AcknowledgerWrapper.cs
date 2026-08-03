using System;
using Pontifex.Utils;

namespace Pontifex.Raw.Reliable.Ack.Protocols
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

    public abstract class AcknowledgerWrapper : IRawServerAcknowledger<IRawReliableAckServerHandler>
    {
        private Action<string> _onFail = null!;
        private IRawServerAcknowledger<IRawReliableAckServerHandler> _wrappedAcknowledger = null!;

        public void Init(IRawServerAcknowledger<IRawReliableAckServerHandler> wrappedAcknowledger, Action<string> onFail)
        {
            _onFail = onFail;
            _wrappedAcknowledger = wrappedAcknowledger;
        }

        public IRawReliableAckServerHandler? TryAck(UnionDataList ackData)
        {
            var wrapper = ConstructWrapper();
            bool isOK = wrapper.CheckAckData(ackData);
            if (isOK)
            {
                IRawReliableAckServerHandler? coreHandler = _wrappedAcknowledger.TryAck(ackData);
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
