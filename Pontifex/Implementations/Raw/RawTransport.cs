using Actuarius.Memory;
using Scriba;

namespace Pontifex.Raw
{
    public abstract class RawTransport : AnyTransport, IRawTransport
    {
        /// <summary>
        /// The maximum single-message size in bytes supported by the transport.
        /// Implemented by concrete transports; it must match the carrier limit.
        /// </summary>
        public abstract int MessageMaxByteSize { get; }
        
        protected new IRawConformanceControl Conformance => (IRawConformanceControl)base.Conformance;
        
        protected RawTransport(string typeName, ILogger logger, IMemoryRental memory, RawConformanceControl conformanceControl) 
            : base(typeName, logger, memory, conformanceControl)
        {
        }
    }
}