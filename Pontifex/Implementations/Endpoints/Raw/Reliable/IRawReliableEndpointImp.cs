namespace Pontifex.Raw.Reliable
{
    /// <summary>
    /// Implementation contract for a reliable raw endpoint that is driven through the endpoint lifecycle.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Other notes</b>: Adds the low-level connection observation that transport
    /// machinery uses to coordinate handshakes with teardown.</description></item>
    /// </list>
    /// </remarks>
    public interface IRawReliableEndpointImp : IRawEndpointImp, IRawReliableEndpoint
    {
        /// <summary>
        /// Gets a value indicating whether the low-level connection has completed and disconnection has not yet
        /// begun.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Must be safe to read concurrently.</description></item>
        /// <item><description><b>Other notes</b>: Unlike <see cref="IRawReliableEndpoint.IsConnected"/> this
        /// ignores protocol stages such as an acknowledgement handshake.</description></item>
        /// </list>
        /// </remarks>
        bool IsLowLevelConnected { get; }
    }
}
