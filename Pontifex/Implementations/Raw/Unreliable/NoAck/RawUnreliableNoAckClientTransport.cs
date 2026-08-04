using Actuarius.Memory;
using Scriba;

namespace Pontifex.Raw.Unreliable.NoAck
{
    public abstract class RawUnreliableNoAckClientTransport : RawUnreliableNoAckTransport
    {
        protected new IRawUnreliableNoAckClientConformanceControl Conformance => (IRawUnreliableNoAckClientConformanceControl)base.Conformance;
     
        protected RawUnreliableNoAckClientTransport(string typeName, ILogger logger, IMemoryRental memory,
            RawUnreliableNoAckClientConformanceControl? conformanceControl = null) 
            : base(typeName, logger, memory, conformanceControl ?? new RawUnreliableNoAckClientConformanceControl())
        {
        }

        protected abstract bool TryMakeReliableForDebug();
        
        protected class RawUnreliableNoAckClientConformanceControl : RawUnreliableNoAckConformanceControl, IRawUnreliableNoAckClientConformanceControl
        {
            public bool TryMakeReliable()
            {
                var owner = (RawUnreliableNoAckClientTransport)_owner;
                return owner.TryMakeReliableForDebug();
            }
        }
    }
}