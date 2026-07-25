using Pontifex.Utils.CheckPointGate;

namespace Pontifex.NoAck.Raw.Unreliable.Tests
{
    /// <summary>
    /// Transport-specific conformance control for NoAckRawUnreliable.
    /// Extends <see cref="IConformanceControl"/> with a checkpoint for the
    /// <c>TrySend</c> running-or-stopping state decision.
    /// </summary>
    public interface INoAckRawUnreliableConformanceControl : IConformanceControl
    {
        /// <summary>
        /// A client or server <c>TrySend</c> call is about to make its
        /// linearized running-or-stopping decision.
        /// </summary>
        /// <remarks>
        /// The checkpoint must be reached before the implementation acquires an
        /// exclusive state lock needed by <c>Stop</c>. This lets a test make either
        /// operation win a send-versus-stop race without manufacturing a deadlock.
        /// A returned gate is inactive until armed by the test. A checkpoint hit
        /// calls <see cref="ICheckPoint.Hit"/> and therefore blocks only while
        /// its gate is armed. All returned gates and this getter are safe for
        /// concurrent use.
        /// </remarks>
        ICheckPoint BeforeTrySendStateDecisionGate { get; }
    }
}
