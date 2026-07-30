using Actuarius.Memory;
using Scriba;

namespace Pontifex.NoAck.Raw.Unreliable
{
    public abstract class NoAckRawUnreliableServerTransport : NoAckRawUnreliableTransport
    {
        protected new INoAckRawUnreliableServerConformanceControl Conformance => (INoAckRawUnreliableServerConformanceControl)base.Conformance;
     
        protected NoAckRawUnreliableServerTransport(string typeName, ILogger logger, IMemoryRental memory,
            NoAckRawUnreliableServerConformanceControl? conformanceControl = null) 
            : base(typeName, logger, memory, conformanceControl ?? new NoAckRawUnreliableServerConformanceControl())
        {
        }
        
        protected class NoAckRawUnreliableServerConformanceControl : NoAckRawUnreliableConformanceControl, INoAckRawUnreliableServerConformanceControl
        {
            
        }
    }
}