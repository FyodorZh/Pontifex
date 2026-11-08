namespace Pontifex.Raw.Reliable.Ack
{
    /// <summary>
    /// Marker interface for a reliable acknowledgement endpoint.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Other notes</b>: Extends <see cref="IRawReliableEndpoint"/> so transport-level
    /// lifetime notifications can expose the acknowledgement-specific endpoint identity without exposing a
    /// concrete endpoint implementation.</description></item>
    /// </list>
    /// </remarks>
    public interface IRawReliableAckEndpoint : IRawReliableEndpoint
    {
    }
}
