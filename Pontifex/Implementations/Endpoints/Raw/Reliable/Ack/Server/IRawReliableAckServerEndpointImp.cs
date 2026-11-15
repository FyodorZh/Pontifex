namespace Pontifex.Raw.Reliable.Ack
{
    /// <summary>
    /// Implementation contract for a reliable server endpoint that performs an acknowledgement handshake.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Other notes</b>: The endpoint is created non-started by a server transport after
    /// the acknowledgement request has been read and validated, and is admitted as a session
    /// afterwards.</description></item>
    /// </list>
    /// </remarks>
    public interface IRawReliableAckServerEndpointImp : IRawReliableServerEndpointImp
    {
    }
}
