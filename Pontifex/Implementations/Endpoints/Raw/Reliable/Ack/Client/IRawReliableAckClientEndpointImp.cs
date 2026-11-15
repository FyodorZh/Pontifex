namespace Pontifex.Raw.Reliable.Ack
{
    /// <summary>
    /// Implementation contract for a reliable client endpoint that performs an acknowledgement handshake
    /// before the logical connection is established. Extends the reliable client endpoint with a logical stage
    /// that sends an ack request and waits for the server's ack response.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Thread safety</b>: All members are safe to call concurrently from any thread. The
    /// ack stage is volatile, and handshake completion and handler notifications are marshalled onto the serial
    /// action queue.</description></item>
    /// <item><description><b>Other notes</b>: The endpoint is created non-started by a client transport and
    /// started during the connect transition. Implementations complete teardown from their disconnect stage; an
    /// ack response received outside the expected stage is treated as a fatal protocol
    /// violation.</description></item>
    /// </list>
    /// </remarks>
    public interface IRawReliableAckClientEndpointImp : IRawReliableClientEndpointImp
    {
    }
}
