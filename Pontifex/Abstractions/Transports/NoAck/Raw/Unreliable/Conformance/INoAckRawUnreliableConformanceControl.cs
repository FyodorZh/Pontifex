using System;
using Pontifex.Utils.CheckPointGate;

namespace Pontifex.NoAck.Raw.Unreliable
{
    /// <summary>
    /// Transport-specific conformance control for NoAckRawUnreliable.
    /// </summary>
    public interface INoAckRawUnreliableConformanceControl : INoAckRawConformanceControl
    {
        /// <summary>
        /// A client or server is about to send data to underlying IO transport
        /// </summary>
        ICheckPoint BeforeSendCommitGate { get; }

        /// <summary>
        /// A client or server has just sent data to underlying IO transport
        /// </summary>
        ICheckPoint AfterSendCommitGate { get; }
        
        /// <summary>
        /// A client or server received data from underlying IO transport
        /// </summary>
        ICheckPoint AfterReceivedGate { get; }
    }
}
