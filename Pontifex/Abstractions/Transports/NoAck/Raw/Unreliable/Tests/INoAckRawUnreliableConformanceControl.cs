using Pontifex.Utils.CheckPointGate;

namespace Pontifex.NoAck.Raw.Unreliable
{
    /// <summary>
    /// Transport-specific conformance control for NoAckRawUnreliable.
    /// Extends <see cref="IConformanceControl"/> with a checkpoint for the
    /// <c>TrySend</c> running-or-stopping state decision.
    /// </summary>
    public interface INoAckRawUnreliableConformanceControl : INoAckRawConformanceControl
    {
    }
}
