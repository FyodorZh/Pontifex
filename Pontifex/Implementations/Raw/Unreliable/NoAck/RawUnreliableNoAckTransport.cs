using Actuarius.Memory;
using Pontifex.Utils.CheckPointGate;
using Scriba;

namespace Pontifex.Raw.Unreliable.NoAck
{
    public abstract class RawUnreliableNoAckTransport : RawUnreliableTransport
    {
        protected new IRawUnreliableNoAckConformanceControl Conformance => (IRawUnreliableNoAckConformanceControl)base.Conformance;
        
        protected RawUnreliableNoAckTransport(string typeName, ILogger logger, IMemoryRental memory, RawUnreliableNoAckConformanceControl? conformanceControl = null) 
            : base(typeName, logger, memory, conformanceControl)
        {
        }

        protected abstract class RawUnreliableNoAckConformanceControl : RawUnreliableConformanceControl, IRawUnreliableNoAckConformanceControl
        {
            private readonly CheckPoint _beforeSendCommitGate = new();
            private readonly CheckPoint _afterSendCommitGate = new();
            private readonly CheckPoint _afterReceivedGate = new();

            public ICheckPointCtl BeforeSendCommitGate => _beforeSendCommitGate;
            public ICheckPointCtl AfterSendCommitGate => _afterSendCommitGate;

            public ICheckPointCtl AfterReceivedGate => _afterReceivedGate;
        }
    }
}
