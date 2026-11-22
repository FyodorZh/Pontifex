using Pontifex.Utils.CheckPointGate;

namespace Pontifex.Raw.Unreliable
{
    /// <summary>
    /// Test-only conformance control for a single IRawUnreliableEndpoint.
    /// All checkpoint gates are inactive until armed by a conformance adapter.
    /// </summary>
    public sealed class RawUnreliableEndpointConformanceControl : IRawUnreliableEndpointConformanceControl
    {
        private readonly CheckPoint _beforeEndpointStopStateTransitionGate = new();
        private readonly CheckPoint _beforeHandlerStoppedGate = new();
        private readonly CheckPoint _beforeSendCommitGate = new();
        private readonly CheckPoint _afterSendCommitGate = new();
        private readonly CheckPoint _afterReceivedGate = new();

        public string Name => "ConformanceControl(RawUnreliableEndpoint)";

        public ICheckPointCtl BeforeEndpointStopStateTransitionGate => _beforeEndpointStopStateTransitionGate;

        public ICheckPointCtl BeforeHandlerStoppedGate => _beforeHandlerStoppedGate;

        public ICheckPointCtl BeforeSendCommitGate => _beforeSendCommitGate;

        public ICheckPointCtl AfterSendCommitGate => _afterSendCommitGate;

        public ICheckPointCtl AfterReceivedGate => _afterReceivedGate;
    }
}
