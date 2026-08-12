using Pontifex.Utils;

namespace Pontifex.Raw
{
    public interface IRawHandler : IHandler
    {
        /// <summary>
        /// Called when data arrives from the remote peer.
        /// </summary>
        void OnReceived(UnionDataList receivedBuffer);

        /// <summary>
        /// Called once when the connection, session, or endpoint is finally
        /// destroyed and becomes invalid.
        /// For a reliable connection that was previously connected, the call
        /// sequence is OnDisconnected() followed by OnStopped(). If OnConnected()
        /// was never triggered, only OnStopped() is called.
        /// For an unreliable endpoint, OnStopped() is called exactly once after
        /// the endpoint becomes invalid, provided OnStarted() completed
        /// successfully; no OnReceived will begin after this point.
        /// </summary>
        /// <param name="reason">The reason for the stop.</param>
        void OnStopped(StopReason reason);
    }
}
