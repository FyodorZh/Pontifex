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

    public abstract class AcknowledgerWrapper : IRawReliableAckServerAcknowledger<IRawReliableAckServerHandler>
    {
        private Action<string> _onFail = null!;
        private IRawReliableAckServerAcknowledger<IRawReliableAckServerHandler> _wrappedAcknowledger = null!;

        public void Init(IRawReliableAckServerAcknowledger<IRawReliableAckServerHandler> wrappedAcknowledger, Action<string> onFail)
        {
            _onFail = onFail;
            _wrappedAcknowledger = wrappedAcknowledger;
        }

        public (IRawReliableAckServerHandler handler, UnionDataList ackResponse)? TryAck(
            IEndPoint remote, UnionDataList ackData)
        {
            IHandlerWrapper wrapper;
            try
            {
                wrapper = ConstructWrapper();
                if (!wrapper.CheckAckData(ackData))
                {
                    // This class is the acknowledger for the received reference: reject it here.
                    ackData.Release();
                    return null;
                }
            }
            catch
            {
                ackData.Release();
                throw;
            }

            // Ownership of the additional ackData reference transfers to the wrapped acknowledger.
            var result = _wrappedAcknowledger.TryAck(remote, ackData);
            if (result == null)
            {
                return null;
            }

            var handler = result.Value.handler;
            var ackResponse = result.Value.ackResponse;

            if (handler == null)
            {
                ackResponse?.Release();
                return null;
            }

            if (ackResponse == null)
            {
                handler.OnStopped(new StopReasons.TextFail("RawReliableAck", "Acknowledger returned no ACK response"));
                return null;
            }

            wrapper.Init(handler.Test(_onFail).GetSafe(e => _onFail(e.ToString())));
            return (wrapper, ackResponse);
        }

        protected abstract IHandlerWrapper ConstructWrapper();
    }
}
