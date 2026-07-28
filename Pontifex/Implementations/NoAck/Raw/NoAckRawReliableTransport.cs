using Actuarius.Memory;
using Pontifex.Utils.CheckPointGate;
using Scriba;

namespace Pontifex.NoAck.Raw
{
    public abstract class NoAckRawTransport : AnyTransport
    {
        private readonly NoAckRawConformanceControl _conformanceControl;

        protected INoAckRawConformanceControl Conformance => _conformanceControl;
        
        protected NoAckRawTransport(string typeName, ILogger logger, IMemoryRental memory, NoAckRawConformanceControl? conformanceControl = null) 
            : base(typeName, logger, memory, conformanceControl)
        {
            _conformanceControl = conformanceControl ?? new NoAckRawConformanceControl(this);
        }

        protected class NoAckRawConformanceControl : ConformanceControl, INoAckRawConformanceControl
        {
            private readonly CheckPoint _beforeTrySendStateDecisionGate;
            
            public NoAckRawConformanceControl(NoAckRawTransport owner) 
                : base(owner)
            {
                _beforeTrySendStateDecisionGate = new CheckPoint();
            }

            public ICheckPoint BeforeTrySendStateDecisionGate => _beforeTrySendStateDecisionGate;
        }
    }
}