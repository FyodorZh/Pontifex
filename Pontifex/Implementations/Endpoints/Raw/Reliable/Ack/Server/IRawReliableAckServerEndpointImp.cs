namespace Pontifex.Raw.Reliable.Ack
{
    /// <summary>
    /// Implementation contract for a reliable server endpoint that completes the acknowledgement handshake
    /// before the logical connection is established. Extends the reliable server endpoint with a logical stage
    /// that answers the client's ack request with an ack response.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Thread safety</b>: All members are safe to call concurrently from any thread. The
    /// ack stage is volatile, and handshake completion and handler notifications are marshalled onto the serial
    /// action queue.</description></item>
    /// <item><description><b>Other notes</b>: The endpoint is created non-started by a server transport after
    /// the acknowledgement request has been read and validated, and is admitted as a session afterwards.
    /// Implementations complete teardown from their disconnect stage; an ack request received outside the
    /// expected stage is treated as a fatal protocol violation. The client's acknowledgement data is released by
    /// the endpoint; validating it is the responsibility of the caller that created the session
    /// handler.</description></item>
    /// </list>
    /// </remarks>
    public interface IRawReliableAckServerEndpointImp : IRawReliableServerEndpointImp
    {
    }
}
