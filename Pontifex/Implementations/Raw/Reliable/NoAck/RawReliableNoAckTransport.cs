using Actuarius.Memory;
using Pontifex.Raw.Unreliable.NoAck;
using Pontifex.Utils.CheckPointGate;
using Scriba;

namespace Pontifex.Raw.Reliable.NoAck
{
    public abstract class RawReliableNoAckTransport : AnyTransport
    {
        protected new IRawReliableNoAckConformanceControl Conformance => (IRawReliableNoAckConformanceControl)base.Conformance;
        
        protected RawReliableNoAckTransport(string typeName, ILogger logger, IMemoryRental memory, RawReliableNoAckConformanceControl? conformanceControl = null) 
            : base(typeName, logger, memory, conformanceControl ?? new RawReliableNoAckConformanceControl())
        {
        }

        protected class RawReliableNoAckConformanceControl : ConformanceControl, IRawReliableNoAckConformanceControl
        {
        }
    }
}