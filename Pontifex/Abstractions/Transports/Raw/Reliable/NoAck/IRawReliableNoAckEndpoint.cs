namespace Pontifex.Raw.Reliable.NoAck
{
    /// <summary>
    /// Marker interface for a reliable no-acknowledgement endpoint.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Other notes</b>: Extends <see cref="IRawReliableEndpoint"/> so endpoint
    /// implementation contracts can expose the no-acknowledgement endpoint identity without exposing a
    /// concrete endpoint implementation.</description></item>
    /// </list>
    /// </remarks>
    public interface IRawReliableNoAckEndpoint : IRawReliableEndpoint
    {
    }
}
