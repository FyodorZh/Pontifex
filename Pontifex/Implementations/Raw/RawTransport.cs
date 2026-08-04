using Actuarius.Memory;
using Scriba;

namespace Pontifex.Raw
{
    public abstract class RawTransport : AnyTransport
    {
        protected new IRawConformanceControl Conformance => (IRawConformanceControl)base.Conformance;
        
        protected RawTransport(string typeName, ILogger logger, IMemoryRental memory, RawConformanceControl? conformanceControl = null) 
            : base(typeName, logger, memory, conformanceControl ?? new RawConformanceControl())
        {
        }

        protected class RawConformanceControl : ConformanceControl, IRawConformanceControl
        {
        }
    }
}