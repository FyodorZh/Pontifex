using Actuarius.Memory;
using Scriba;

namespace Pontifex.Raw.Reliable
{
    public abstract class RawReliableTransport : RawTransport, IRawReliableTransport
    {
        protected new IRawConformanceControl Conformance => (IRawReliableConformanceControl)base.Conformance;
        
        protected RawReliableTransport(string typeName, ILogger logger, IMemoryRental memory, RawReliableConformanceControl conformanceControl) 
            : base(typeName, logger, memory, conformanceControl)
        {
        }
    }
}