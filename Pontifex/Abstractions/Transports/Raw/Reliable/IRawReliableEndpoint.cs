using Pontifex.Utils;

namespace Pontifex.Raw.Reliable
{
    public interface IRawReliableEndpoint : IRawEndpoint
    {
        /// <summary>
        /// Gets whether the endpoint is currently connected.
        /// May return true before OnConnected() is invoked (the endpoint reference
        /// is not available to the caller before OnConnected()).
        /// Guaranteed to return false during and after OnDisconnected() — the
        /// IsConnected transition to false is synchronized with the OnDisconnected() call.
        /// Safe to read concurrently.
        /// </summary>
        bool IsConnected { get; }
        
        /// <summary>
        /// Initiates a logical disconnection of this endpoint with the given reason.
        /// </summary>
        /// <param name="reason">The reason for the disconnection.</param>
        /// <returns>True if the disconnect was initiated successfully.</returns>
        bool Disconnect(StopReason reason);
    }
}