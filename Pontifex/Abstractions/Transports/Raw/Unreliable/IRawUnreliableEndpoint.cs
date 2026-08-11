using Pontifex.Utils;

namespace Pontifex.Raw.Unreliable
{
    public interface IRawUnreliableEndpoint : IRawEndpoint
    {
        /// <summary>
        /// Gets whether the endpoint is currently valid and may accept sends.
        /// Safe to read concurrently.
        /// </summary>
        bool IsValid { get; }

        /// <summary>
        /// Stops this endpoint. Returns true for the one call that begins stopping a
        /// valid endpoint; false for all later calls. Null reason maps to a
        /// transport-generated Unknown reason supplied to OnStopped.
        /// </summary>
        bool Stop(StopReason? reason = null);
    }
}
