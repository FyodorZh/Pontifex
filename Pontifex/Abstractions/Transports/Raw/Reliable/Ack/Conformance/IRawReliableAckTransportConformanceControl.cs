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
        /// Hit once immediately after the acknowledger's <c>TryAck</c> returns
        /// the ACK response and before that response is accepted for outbound
        /// delivery.
        /// Not hit for a client transport. If this gate blocks, the server
        /// <c>OnConnected</c> cannot be invoked. It is not hit when
        /// <c>TryAck</c> fails or the ACK response is oversized.
        /// </summary>
        /// <remarks>
        /// The ACK response is built by <c>TryAck</c>, not by a handler callback.
        /// No production implementation hits this gate yet; it remains part of
        /// the ignored conformance work and is recorded as a known coverage gap.
        /// </remarks>
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
