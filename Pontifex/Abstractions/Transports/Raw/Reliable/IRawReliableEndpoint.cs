namespace Pontifex.Raw.Reliable
{
    /// <summary>
    /// Reliable raw endpoint contract: the raw endpoint surface plus a logical connection stage and an explicit
    /// disconnect request.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Side effects</b>: Outbound payloads are framed with a leading control byte before
    /// admission, so the buffer is modified even when admission later fails. When a send returns
    /// InvalidMessage the buffer is null or not alive and cannot be transferred, so the caller retains
    /// ownership.</description></item>
    /// </list>
    /// </remarks>
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
        /// TODO: remove bool result
        bool Disconnect(StopReason reason);
    }
}