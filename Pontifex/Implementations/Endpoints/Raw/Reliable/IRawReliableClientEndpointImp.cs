namespace Pontifex.Raw.Reliable
{
    /// <summary>
    /// Implementation contract for a reliable client endpoint driven by a client transport.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Other notes</b>: A client transport attaches exactly one such endpoint and
    /// delegates connectivity and handler notifications to it.</description></item>
    /// </list>
    /// </remarks>
    public interface IRawReliableClientEndpointImp : IRawReliableEndpointImp, IRawReliableClientEndpoint
    {
    }
}
