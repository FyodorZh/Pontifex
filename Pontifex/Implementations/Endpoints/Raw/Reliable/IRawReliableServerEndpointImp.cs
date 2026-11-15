namespace Pontifex.Raw.Reliable
{
    /// <summary>
    /// Implementation contract for a reliable server endpoint. Extends the reliable endpoint with an
    /// idle-session watchdog: inbound traffic refreshes a timestamp, and the driver-invoked
    /// <see cref="PeriodicTick"/> stops the endpoint when no inbound message arrives within the configured
    /// timeout.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Thread safety</b>: All members are safe to call concurrently from any thread. The
    /// watchdog timestamp is accessed through a thread-safe atomic box and the stop request is
    /// idempotent.</description></item>
    /// <item><description><b>Other notes</b>: A server transport starts and registers the endpoint, ticks it
    /// periodically, and releases its session slot when <see cref="IRawEndpointImp.Stopped"/> is raised.
    /// Implementations are designed to work with a reliable client endpoint that sends ping frames, which this
    /// endpoint answers with pong frames; the server disconnect timeout is intended to equal the client
    /// disconnect timeout.</description></item>
    /// </list>
    /// </remarks>
    public interface IRawReliableServerEndpointImp : IRawReliableEndpointImp, IRawReliableServerEndpoint
    {
        /// <summary>
        /// Advances the idle-session watchdog, stopping the endpoint with a timeout when no inbound message has
        /// arrived within the configured disconnect timeout.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Safe to call concurrently from any thread.</description></item>
        /// <item><description><b>Other notes</b>: Intended to be called periodically by the transport driver.
        /// The call is a no-op unless the endpoint is connected and its watchdog timestamp has been initialized.
        /// The effective timeout is the configured timeout rounded up to the tick interval.</description></item>
        /// </list>
        /// </remarks>
        void PeriodicTick();
    }
}
