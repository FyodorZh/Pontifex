using Actuarius.Memory;
using Pontifex.NoAck.Raw.Unreliable;
using Pontifex.Utils.CheckPointGate;
using Scriba;

namespace Pontifex.NoAck.Raw.Reliable
{
    public abstract class NoAckRawTransport : AnyTransport
    {
        protected new INoAckRawConformanceControl Conformance => (INoAckRawConformanceControl)base.Conformance;
        
        protected NoAckRawTransport(string typeName, ILogger logger, IMemoryRental memory, NoAckRawConformanceControl? conformanceControl = null) 
            : base(typeName, logger, memory, conformanceControl ?? new NoAckRawConformanceControl())
        {
        }

        protected class NoAckRawConformanceControl : ConformanceControl, INoAckRawConformanceControl
        {
        }
    }
}