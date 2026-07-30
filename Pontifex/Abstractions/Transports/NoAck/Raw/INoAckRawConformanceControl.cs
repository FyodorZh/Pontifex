using Pontifex.Utils.CheckPointGate;

namespace Pontifex.NoAck.Raw
{
    public interface INoAckRawConformanceControl : IConformanceControl
    {
        /// <summary>
        /// A client or server is about to send data to underlying IO transport
        /// </summary>
        ICheckPoint BeforeSendCommitGate { get; }

        /// <summary>
        /// A client or server has just sent data to underlying IO transport
        /// </summary>
        ICheckPoint AfterSendCommitGate { get; }
    }
}