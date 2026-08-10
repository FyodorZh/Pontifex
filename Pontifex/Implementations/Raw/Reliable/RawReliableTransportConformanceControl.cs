using Pontifex.Raw.Reliable.Ack;
using Pontifex.Utils.CheckPointGate;

namespace Pontifex.Raw.Reliable
{
    /// <summary>
    /// Test-only conformance control for a RawReliable transport. All
    /// checkpoint gates are inactive until armed by a conformance adapter.
    /// </summary>
    public class RawReliableTransportConformanceControl : RawConformanceControl, IRawReliableAckTransportConformanceControl
    {
        private readonly CheckPoint _beforeAcknowledgerGate = new();
        private readonly CheckPoint _beforeAckResponseCommitGate = new();
        private readonly CheckPoint _beforeHandlerConnectedGate = new();

        public ICheckPointCtl BeforeAcknowledgerGate => _beforeAcknowledgerGate;

        public ICheckPointCtl BeforeAckResponseCommitGate => _beforeAckResponseCommitGate;

        public ICheckPointCtl BeforeHandlerConnectedGate => _beforeHandlerConnectedGate;
    }
}
