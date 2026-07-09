using Pontifex.Utils;

namespace Pontifex.NoAck.Raw_old
{
    /// <summary>
    /// Client-side handler for NoAck raw transport.
    /// </summary>
    public interface INoAckRawClientSideHandler : IHandler
    {
        /// <summary>
        /// Called after the transport is fully initialized.
        /// </summary>
        /// <param name="endpoint">The configured and ready-to-use endpoint for sending messages.</param>
        void OnStarted(INoAckRawClientSideEndpoint endpoint);

        /// <summary>
        /// Called when a message is received from the server.
        /// Starts working after OnStarted().
        /// </summary>
        /// <param name="message">The data sent by the server. Ownership is transferred to the handler.</param>
        void OnReceived(UnionDataList message);
        
        /// <summary>
        /// Called when the transport is destroyed. Invoked strictly after OnStarted().
        /// The endpoint becomes invalid.
        /// </summary>
        void OnStopped(StopReason reason);
    }
}