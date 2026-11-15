namespace Pontifex.Raw.Reliable.Ack
{
    /// <summary>
    /// Implementation contract for a reliable client endpoint that performs an acknowledgement handshake.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Other notes</b>: The endpoint is created non-started by a client transport and
    /// started during the connect transition.</description></item>
    /// </list>
    /// </remarks>
    public interface IRawReliableAckClientEndpointImp : IRawReliableClientEndpointImp
    {
    }
}
