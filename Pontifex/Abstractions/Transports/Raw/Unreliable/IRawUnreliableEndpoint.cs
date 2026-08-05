using Pontifex.Utils;

namespace Pontifex.Raw.Unreliable
{
    public interface IRawUnreliableEndpoint : IRawEndpoint
    {
        bool IsValid { get; }

        /// <summary>
        /// Attempts to send a message to the remote route of this endpoint.
        /// Ownership transfers to the transport for every non-null message argument,
        /// regardless of the result. Success indicates local acceptance only; actual
        /// delivery is not verifiable and loss/reorder/duplication are possible.
        /// </summary>
        SendResult UnreliableSend(UnionDataList message);

        /// <summary>
        /// Stops this endpoint. Returns true for the one call that begins stopping a
        /// valid endpoint; false for all later calls. Null reason maps to a
        /// transport-generated Unknown reason supplied to OnStopped.
        /// </summary>
        bool Stop(StopReason? reason = null);
    }
}
