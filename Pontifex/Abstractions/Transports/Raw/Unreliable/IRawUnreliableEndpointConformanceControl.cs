using Pontifex.Utils.CheckPointGate;

namespace Pontifex.Raw.Unreliable
{
    /// <summary>
    /// Test-only control for a single IRawUnreliableEndpoint, obtained via
    /// IRawUnreliableEndpoint.GetControls after the endpoint is received in
    /// OnStarted. Shared by the Ack and NoAck contract variants.
    /// </summary>
    public interface IRawUnreliableEndpointConformanceControl : IControl
    {
        /// <summary>Hit when a valid endpoint is about to transition to invalid.</summary>
        ICheckPointCtl BeforeEndpointStopStateTransitionGate { get; }

        /// <summary>Hit once immediately before the endpoint invokes handler.OnStopped.</summary>
        ICheckPointCtl BeforeHandlerStoppedGate { get; }

        /// <summary>Hit when a message accepted from this endpoint is about to reach an underlying IO commit.</summary>
        ICheckPointCtl BeforeSendCommitGate { get; }

        /// <summary>Hit after an accepted message completes an underlying IO commit attempt.</summary>
        ICheckPointCtl AfterSendCommitGate { get; }

        /// <summary>Hit once per impending OnReceived invocation for this endpoint, immediately before it begins.</summary>
        ICheckPointCtl AfterReceivedGate { get; }
    }
}
