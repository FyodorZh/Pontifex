using Pontifex.Utils.CheckPointGate;

namespace Pontifex.Raw.Reliable.Ack
{
    /// <summary>
    /// Test-only deterministic control over a RawReliableAck transport instance.
    /// Obtained through <see cref="ITransport.GetControls"/> before starting the
    /// transport. Required only from implementations claiming Carrier-Independent
    /// Core Conformance.
    /// </summary>
    public interface IRawReliableAckTransportConformanceControl : IConformanceControl
    {
        /// <summary>
        /// Hit once immediately before each server <c>TryAck</c> invocation.
        /// Not hit for malformed, oversized, stopped, or otherwise discarded
        /// connection attempts. Not hit for a client transport. The gate
        /// participates in the global acknowledger serialization rule.
        /// </summary>
        ICheckPointCtl BeforeAcknowledgerGate { get; }

        /// <summary>
        /// Hit once immediately after a server handler's <c>FillAckResponse</c>
        /// returns and before the ACK response is accepted for outbound delivery.
        /// Not hit for a client transport. If this gate blocks, the server
        /// <c>OnConnected</c> cannot be invoked. If <c>FillAckResponse</c> throws
        /// or the ACK response is oversized, this gate is not hit.
        /// </summary>
        ICheckPointCtl BeforeAckResponseCommitGate { get; }

        /// <summary>
        /// Hit once immediately before a handler's <c>OnConnected</c> invocation.
        /// Hit for both client and server handlers after the handshake has
        /// succeeded and the endpoint is valid, but before application callback
        /// execution. Not hit when a session is rejected or when establishment
        /// fails.
        /// </summary>
        ICheckPointCtl BeforeHandlerConnectedGate { get; }
    }
}
