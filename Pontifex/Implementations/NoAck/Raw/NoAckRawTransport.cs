using Actuarius.Memory;
using Pontifex.NoAck.Raw.Unreliable;
using Pontifex.Utils.CheckPointGate;
using Scriba;

namespace Pontifex.NoAck.Raw
{
    public abstract class NoAckRawTransport : AnyTransport
    {
        protected new INoAckRawConformanceControl Conformance => (INoAckRawConformanceControl)base.Conformance;
        
        protected NoAckRawTransport(string typeName, ILogger logger, IMemoryRental memory, NoAckRawConformanceControl? conformanceControl = null) 
            : base(typeName, logger, memory, conformanceControl ?? new NoAckRawConformanceControl())
        {
        }

        protected class NoAckRawConformanceControl : ConformanceControl, INoAckRawConformanceControl, INoAckRawUnreliableConformanceControl
        {
            private readonly CheckPoint _beforeSendCommitGate = new();
            
            private readonly CheckPoint _afterSendCommitGate = new();

            public ICheckPoint BeforeSendCommitGate => _beforeSendCommitGate;

            public ICheckPoint AfterSendCommitGate => _afterSendCommitGate;
        }
    }
}