namespace Pontifex.Raw.Reliable
{
    /// <summary>
    /// Identifies a reliable client endpoint.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Other notes</b>: Marker interface that groups the client-side end of a reliable
    /// connection. It exposes no additional members beyond
    /// <see cref="IRawReliableEndpoint"/>.</description></item>
    /// </list>
    /// </remarks>
    public interface IRawReliableClientEndpoint : IRawReliableEndpoint
    {
    }
}
