using Pontifex.Utils.CheckPointGate;

namespace Pontifex.Raw.Unreliable.NoAck
{
    /// <summary>
    /// Transport-specific conformance control for RawUnreliableNoAck.
    /// </summary>
    public interface IRawUnreliableNoAckConformanceControl : IRawNoAckConformanceControl
    {
        /// <summary>
        /// An accepted message is about to reach an underlying IO commit attempt.
        /// Synchronously rejected messages and accepted messages discarded before
        /// a commit attempt do not hit this gate.
        /// </summary>
        ICheckPointCtl BeforeSendCommitGate { get; }

        /// <summary>
        /// An accepted message has completed an underlying IO commit attempt.
        /// </summary>
        ICheckPointCtl AfterSendCommitGate { get; }
        
        /// <summary>
        /// A valid, routed inbound message has been bound to the current sole
        /// receive handler and is about to invoke that handler. The gate is hit
        /// once per impending OnReceived invocation, immediately before it begins.
        /// It is not hit for malformed, oversized, stopped or discarded messages,
        /// or when no receive handler is attached.
        /// </summary>
        ICheckPointCtl AfterReceivedGate { get; }
    }
}
