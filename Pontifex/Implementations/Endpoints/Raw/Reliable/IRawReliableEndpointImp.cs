namespace Pontifex.Raw.Reliable
{
    /// <summary>
    /// Implementation contract for a reliable raw endpoint. Extends the raw endpoint lifecycle with a logical
    /// connection stage machine: connection completion, graceful and ungraceful disconnection, framing of
    /// outbound payloads, and dispatch of inbound control frames.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Thread safety</b>: All members are safe to call concurrently from any thread.
    /// Connection stages, handler dispatch, and payload transmission are serialized, and the stage fields are
    /// volatile.</description></item>
    /// <item><description><b>Preconditions/Postconditions</b>: The lifecycle is one-shot. After the endpoint
    /// reaches the disconnected or stopped state it cannot be connected again.</description></item>
    /// <item><description><b>Other notes</b>: Implementations supply the transport-specific connect and
    /// disconnect work and complete it through the endpoint's connection stages.</description></item>
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
        /// <item><description><b>Thread safety</b>: Safe to read concurrently.</description></item>
        /// <item><description><b>Other notes</b>: Unlike <see cref="IRawReliableEndpoint.IsConnected"/> this
        /// ignores protocol stages such as an acknowledgement handshake. Becomes true only when the connection
        /// completes and becomes false as soon as disconnection begins, before the handler's disconnect
        /// notification is raised.</description></item>
        /// </list>
        /// </remarks>
        bool IsLowLevelConnected { get; }
    }
}
