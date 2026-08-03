using Actuarius.Memory;
using Scriba;

namespace Pontifex.Raw.Unreliable.NoAck
{
    public abstract class RawUnreliableNoAckServerTransport : RawUnreliableNoAckTransport
    {
        protected new IRawUnreliableNoAckServerConformanceControl Conformance => (IRawUnreliableNoAckServerConformanceControl)base.Conformance;
     
        protected RawUnreliableNoAckServerTransport(string typeName, ILogger logger, IMemoryRental memory,
            RawUnreliableNoAckServerConformanceControl? conformanceControl = null) 
            : base(typeName, logger, memory, conformanceControl ?? new RawUnreliableNoAckServerConformanceControl())
        {
        }
        
        protected class RawUnreliableNoAckServerConformanceControl : RawUnreliableNoAckConformanceControl, IRawUnreliableNoAckServerConformanceControl
        {
            
        }
    }
}