namespace Pontifex.Raw.Reliable.NoAck
{
    /// <summary>
    /// Implementation contract for a reliable no-acknowledgement client endpoint. Extends the reliable client
    /// endpoint with the logical connection and disconnect notifications that the endpoint owns.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Thread safety</b>: All members are safe to call concurrently from any thread. The
    /// notification latches are atomic, and handler notifications are marshalled onto the serial action
    /// queue.</description></item>
    /// <item><description><b>Other notes</b>: The endpoint is created non-started by a client transport and
    /// started during the connect transition. There is no acknowledgement step: the logical connection
    /// completes with the low-level connection. Implementations complete teardown from their disconnect
    /// stage.</description></item>
    /// </list>
    /// </remarks>
    public interface IRawReliableNoAckClientEndpointImp : IRawReliableClientEndpointImp
    {
    }
}
