using Pontifex.Utils.CheckPointGate;

namespace Pontifex.Raw.Unreliable
{
    /// <summary>
    /// Test-only conformance control for a RawUnreliable transport. All
    /// checkpoint gates are inactive until armed by a conformance adapter.
    /// </summary>
    public class RawUnreliableTransportConformanceControl : RawUnreliableConformanceControl, IRawUnreliableTransportConformanceControl
    {
        private readonly CheckPoint _beforeHandlerFactoryGate = new();
        private readonly CheckPoint _beforeHandlerStartedGate = new();

        public ICheckPointCtl BeforeHandlerFactoryGate => _beforeHandlerFactoryGate;

        public ICheckPointCtl BeforeHandlerStartedGate => _beforeHandlerStartedGate;

        /// <summary>
        /// Enables transport-wide reliable debug mode by forwarding the request to the carrier.
        /// </summary>
        /// <returns>The carrier's answer, or <see langword="false"/> when the owner does not implement
        /// <see cref="IRawUnreliableDebugReliability"/> and therefore has no debug reliability mode.
        /// </returns>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Other notes</b>: An owner without the interface is not an error; the
        /// request is declined instead of throwing.</description></item>
        /// </list>
        /// </remarks>
        public bool TryMakeReliable()
        {
            return _owner is IRawUnreliableDebugReliability debugReliability
                   && debugReliability.TryMakeReliableForDebug();
        }
    }
}
