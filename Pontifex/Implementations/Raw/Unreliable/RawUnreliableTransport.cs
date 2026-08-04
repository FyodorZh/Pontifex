using Actuarius.Memory;
using Scriba;

namespace Pontifex.Raw.Unreliable
{
    public abstract class RawUnreliableTransport : RawTransport
    {
        protected new IRawUnreliableConformanceControl Conformance => (IRawUnreliableConformanceControl)base.Conformance;
        
        protected RawUnreliableTransport(string typeName, ILogger logger, IMemoryRental memory, RawUnreliableConformanceControl? conformanceControl = null) 
            : base(typeName, logger, memory, conformanceControl ?? new RawUnreliableConformanceControl())
        {
        }

        protected class RawUnreliableConformanceControl : RawConformanceControl, IRawUnreliableConformanceControl
        {
        }
    }
}