using Pontifex.Raw.Unreliable.Ack;
using Pontifex.Raw.Unreliable.NoAck;
using Pontifex.Utils.CheckPointGate;

namespace Pontifex.Raw.Unreliable
{
    /// <summary>
    /// Test-only conformance control for a RawUnreliable transport.
    /// Implements both the Ack and NoAck contract variants. All checkpoint
    /// gates are inactive until armed by a conformance adapter.
    /// </summary>
    public class RawUnreliableTransportConformanceControl : RawUnreliableConformanceControl, IRawUnreliableNoAckTransportConformanceControl, IRawUnreliableAckTransportConformanceControl
    {
        private readonly CheckPoint _beforeHandlerFactoryGate = new();
        private readonly CheckPoint _beforeHandlerStartedGate = new();

        public ICheckPointCtl BeforeHandlerFactoryGate => _beforeHandlerFactoryGate;

        public ICheckPointCtl BeforeHandlerStartedGate => _beforeHandlerStartedGate;

        public bool TryMakeReliable()
        {
            // Stage 4 keeps both lookup paths: the carriers still implement the legacy protected internal
            // member, while the new architecture extracts it into IRawUnreliableDebugReliability. The
            // legacy fallback is deleted in Stage 6.
            if (_owner is IRawUnreliableDebugReliability debugReliability)
            {
                return debugReliability.TryMakeReliableForDebug();
            }

            return ((LegacyRawUnreliableTransport)_owner).TryMakeReliableForDebug();
        }
    }
}
