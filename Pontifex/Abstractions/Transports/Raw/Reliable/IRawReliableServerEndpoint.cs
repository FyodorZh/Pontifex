namespace Pontifex.Raw.Reliable
{
    /// <summary>
    /// Identifies a reliable server endpoint.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Other notes</b>: A server endpoint is admitted by a server transport and driven
    /// by its shared session ticker.</description></item>
    /// </list>
    /// </remarks>
    public interface IRawReliableServerEndpoint : IRawReliableEndpoint
    {
    }
}
