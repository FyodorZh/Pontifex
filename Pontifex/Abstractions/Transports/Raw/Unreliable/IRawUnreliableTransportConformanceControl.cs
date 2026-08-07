using Pontifex.Utils.CheckPointGate;

namespace Pontifex.Raw.Unreliable
{
    /// <summary>
    /// Test-only control for a RawUnreliable transport instance, obtained via
    /// ITransport.GetControls before starting. Shared by the Ack and NoAck
    /// contract variants.
    /// </summary>
    public interface IRawUnreliableTransportConformanceControl : IRawUnreliableConformanceControl
    {
        /// <summary>Hit once immediately before each server handlerFactory invocation.</summary>
        ICheckPointCtl BeforeHandlerFactoryGate { get; }

        /// <summary>Hit once immediately before an endpoint's handler.OnStarted invocation.</summary>
        ICheckPointCtl BeforeHandlerStartedGate { get; }

        /// <summary>
        /// Enables transport-wide reliable debug mode for every current and future
        /// endpoint route of this transport. Must be called before Start. Returns
        /// false if the implementation cannot provide the test mode.
        /// </summary>
        bool TryMakeReliable();
    }
}
