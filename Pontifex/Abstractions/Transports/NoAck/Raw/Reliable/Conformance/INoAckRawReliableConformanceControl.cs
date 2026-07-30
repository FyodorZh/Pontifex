using Pontifex.Utils.CheckPointGate;

namespace Pontifex.NoAck.Raw.Reliable
{
    public interface INoAckRawReliableConformanceControl : INoAckRawConformanceControl
    {
        ICheckPointCtl BeforeSendCommitGate { get; }

        ICheckPointCtl AfterSendCommitGate { get; }

        ICheckPointCtl AfterReceivedGate { get; }
    }
}
