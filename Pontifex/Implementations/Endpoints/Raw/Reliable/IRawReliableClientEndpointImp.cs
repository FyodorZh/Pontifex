namespace Pontifex.Raw.Reliable
{
    /// <summary>
    /// Implementation contract for a reliable client endpoint. Extends the reliable endpoint with an optional
    /// keep-alive mechanism that sends ping frames and stops the endpoint when no pong frame is received within
    /// the configured timeout.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Thread safety</b>: All members are safe to call concurrently from any thread.
    /// Inbound handling accesses the pong timestamp through a thread-safe atomic box, and the keep-alive loop
    /// runs on a thread-pool thread.</description></item>
    /// <item><description><b>Other notes</b>: A client transport attaches exactly one such endpoint and
    /// delegates connectivity and handler notifications to it. Implementations supply the transport-specific
    /// connect and disconnect work; overrides of the connected and inbound hooks must preserve the keep-alive
    /// timestamp.</description></item>
    /// </list>
    /// </remarks>
    public interface IRawReliableClientEndpointImp : IRawReliableEndpointImp, IRawReliableClientEndpoint
    {
    }
}
