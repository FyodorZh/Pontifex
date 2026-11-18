namespace Pontifex.Raw.Reliable.Ack
{
    /// <summary>
    /// Implementation contract for a reliable server endpoint that completes the acknowledgement handshake
    /// before the logical connection is established. Extends the reliable server endpoint with a logical stage
    /// that sends the acknowledger-built ack response to the client.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Thread safety</b>: All members are safe to call concurrently from any thread. The
    /// ack stage is volatile, and handshake completion and handler notifications are marshalled onto the serial
    /// action queue.</description></item>
    /// <item><description><b>Other notes</b>: The endpoint is created non-started by a server transport with an
    /// already-built ACK response returned by <see cref="IRawReliableAckServerAcknowledger{THandler}.TryAck"/>.
    /// It never receives or releases the client's acknowledgement data, and it is admitted as a session
    /// afterwards. Implementations complete teardown from their disconnect stage; a stray
    /// <c>AckRequestMessage</c> is an unsupported frame that stops the endpoint.</description></item>
    /// </list>
    /// </remarks>
    public interface IRawReliableAckServerEndpointImp : IRawReliableServerEndpointImp
    {
    }
}
