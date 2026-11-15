namespace Pontifex.Raw.Reliable
{
    /// <summary>
    /// Implementation contract for a reliable server endpoint admitted by a server transport.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Other notes</b>: A server transport starts and registers the endpoint, ticks it
    /// periodically, and releases its session slot when <see cref="IRawEndpointImp.Stopped"/> is
    /// raised.</description></item>
    /// </list>
    /// </remarks>
    public interface IRawReliableServerEndpointImp : IRawReliableEndpointImp, IRawReliableServerEndpoint
    {
        /// <summary>
        /// Advances the idle-session watchdog.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Must be safe to call concurrently.</description></item>
        /// <item><description><b>Other notes</b>: Called periodically by the owning transport; implementations
        /// without an idle-timeout policy should treat it as a no-op.</description></item>
        /// </list>
        /// </remarks>
        void PeriodicTick();
    }
}
