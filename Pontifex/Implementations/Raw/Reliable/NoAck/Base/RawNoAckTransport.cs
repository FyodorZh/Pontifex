using Actuarius.Memory;
using Pontifex.Raw.Unreliable.NoAck;
using Pontifex.Utils.CheckPointGate;
using Scriba;

namespace Pontifex.Raw.Reliable.NoAck
{
    public abstract class RawNoAckTransport : AnyTransport
    {
        protected new IRawNoAckConformanceControl Conformance => (IRawNoAckConformanceControl)base.Conformance;
        
        protected RawNoAckTransport(string typeName, ILogger logger, IMemoryRental memory, RawNoAckConformanceControl? conformanceControl = null) 
            : base(typeName, logger, memory, conformanceControl ?? new RawNoAckConformanceControl())
        {
        }

        protected class RawNoAckConformanceControl : ConformanceControl, IRawNoAckConformanceControl
        {
        }
    }
}