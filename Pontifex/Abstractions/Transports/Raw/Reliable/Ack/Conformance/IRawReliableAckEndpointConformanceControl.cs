using Pontifex.Utils;
using Pontifex.Utils.CheckPointGate;

namespace Pontifex.Raw.Reliable.Ack
{
    /// <summary>
    /// Test-only deterministic control over a RawReliableAck endpoint instance.
    /// Obtained through <see cref="IRawReliableEndpoint.GetControls"/> after
    /// receiving that endpoint in <c>OnConnected</c>. Required only from
    /// implementations claiming Carrier-Independent Core Conformance.
    /// </summary>
    public interface IRawReliableAckEndpointConformanceControl : IControl
    {
        /// <summary>
        /// Hit when a connected endpoint is about to transition to disconnected,
        /// whether the cause is <c>Disconnect</c>, local failure, remote failure,
        /// or owning-transport stop. Occurs before that transition becomes visible
        /// to a concurrent <c>IsConnected</c> read.
        /// </summary>
        ICheckPointCtl BeforeEndpointDisconnectStateTransitionGate { get; }

        /// <summary>
        /// Hit once immediately before the endpoint invokes
        /// <c>handler.OnDisconnected(reason)</c>. The endpoint is already
        /// disconnected at this point.
        /// </summary>
        ICheckPointCtl BeforeHandlerDisconnectedGate { get; }

        /// <summary>
        /// Hit once immediately before a client handler's <c>OnStopped(reason)</c>
        /// is invoked. The session has already been disconnected. Not triggered
        /// for server sessions (server handlers do not have <c>OnStopped</c>).
        /// Not hit in the establishment-failure path where <c>OnConnected</c> was
        /// never invoked.
        /// </summary>
        ICheckPointCtl BeforeHandlerStoppedGate { get; }

        /// <summary>
        /// Hit when a message accepted from this endpoint is about to reach an
        /// underlying IO commit attempt. Synchronously rejected messages and
        /// accepted messages discarded before a commit attempt do not hit this
        /// gate.
        /// </summary>
        ICheckPointCtl BeforeSendCommitGate { get; }

        /// <summary>
        /// Hit after an endpoint message completes an underlying IO commit attempt.
        /// </summary>
        ICheckPointCtl AfterSendCommitGate { get; }

        /// <summary>
        /// Hit once per impending <c>OnReceived</c> invocation for this endpoint,
        /// immediately before it begins. Not hit for malformed, oversized,
        /// stopped, discarded, or handshake messages. Not hit for
        /// <c>OnConnected</c>, <c>OnDisconnected</c>, or <c>OnStopped</c>.
        /// </summary>
        ICheckPointCtl AfterReceivedGate { get; }

        /// <summary>
        /// Monotonically-incrementing count of messages that have hit
        /// <see cref="BeforeSendCommitGate"/> for this endpoint.
        /// Safe for concurrent reads.
        /// </summary>
        int BeforeSendCommitHitCount { get; }

        /// <summary>
        /// Monotonically-incrementing count of messages that have hit
        /// <see cref="AfterSendCommitGate"/> for this endpoint.
        /// Safe for concurrent reads.
        /// </summary>
        int AfterSendCommitHitCount { get; }

        /// <summary>
        /// Monotonically-incrementing count of messages that have hit
        /// <see cref="AfterReceivedGate"/> for this endpoint.
        /// Safe for concurrent reads.
        /// </summary>
        int AfterReceivedHitCount { get; }

        /// <summary>
        /// Injects an inbound <c>UnionDataList</c> into the endpoint's receive
        /// path exactly as if it had arrived from the carrier. The implementation
        /// must process it according to the same validation, decoding, size-check,
        /// and delivery rules as a genuine carrier message.
        /// </summary>
        /// <remarks>
        /// <para>
        /// For a valid, well-formed <c>UnionDataList</c> within
        /// <c>MessageMaxByteSize</c>, the implementation must deliver it through
        /// the normal <c>OnReceived</c> path, subject to the same serialization
        /// and ordering guarantees.
        /// </para>
        /// <para>
        /// For malformed, undecodable, or oversized data, the implementation must
        /// discard and log the data, then disconnect the logical connection. The
        /// data must not be delivered through <c>OnReceived</c>. The transport or
        /// server must not be stopped or invalidated solely because of injected
        /// malformed data.
        /// </para>
        /// <para>
        /// This method must not directly invoke application callbacks, fabricate
        /// <c>SendResult</c> values, or intercept genuine carrier traffic. It is
        /// available only on instances created by a conformance adapter. Ordinary
        /// production instances must not expose it.
        /// </para>
        /// </remarks>
        void InjectInboundData(UnionDataList data);
    }
}
