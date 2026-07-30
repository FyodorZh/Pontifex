using Actuarius.Memory;
using Pontifex.NoAck.Raw.Unreliable;
using Pontifex.Utils.CheckPointGate;
using Scriba;

namespace Pontifex.NoAck.Raw.Unreliable
{
    public abstract class NoAckRawUnreliableTransport : NoAckRawTransport
    {
        protected new INoAckRawUnreliableConformanceControl Conformance => (INoAckRawUnreliableConformanceControl)base.Conformance;
        
        protected NoAckRawUnreliableTransport(string typeName, ILogger logger, IMemoryRental memory, NoAckRawUnreliableConformanceControl? conformanceControl = null) 
            : base(typeName, logger, memory, conformanceControl)
        {
        }

        protected abstract class NoAckRawUnreliableConformanceControl : NoAckRawConformanceControl, INoAckRawUnreliableConformanceControl
        {
            private readonly CheckPoint _beforeSendCommitGate = new();
            private readonly CheckPoint _afterSendCommitGate = new();
            private readonly CheckPoint _beforeReceivedGate = new();

            public ICheckPoint BeforeSendCommitGate => _beforeSendCommitGate;
            public ICheckPoint AfterSendCommitGate => _afterSendCommitGate;

            public ICheckPoint AfterReceivedGate => _beforeReceivedGate;
        }
    }
}