namespace Pontifex.Raw.Reliable
{
    /// <summary>
    /// Identifies a reliable server endpoint.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Other notes</b>: A server endpoint is admitted by a server transport, driven by
    /// its shared session ticker, and stopped with the transport.</description></item>
    /// </list>
    /// </remarks>
    public interface IRawReliableServerEndpoint : IRawReliableEndpoint
    {
    }
}
