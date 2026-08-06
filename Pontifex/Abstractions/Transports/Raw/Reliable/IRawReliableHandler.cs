using Pontifex.Utils;

namespace Pontifex.Raw.Reliable
{
    /// <summary>
    /// Base handler for raw-reliable connection lifecycle events.
    /// Implemented by business logic to receive logical disconnect notifications
    /// and incoming data from the transport.
    /// </summary>
    public interface IRawReliableHandler : IRawHandler
    {
        /// <summary>
        /// Logical disconnect. Informs business logic that the logical connection
        /// between server and client is no longer maintained.
        /// The transport may still be physically alive.
        /// OnReceived() will no longer be called after this point.
        /// When this callback is invoked, endpoint.IsConnected is guaranteed to
        /// return false.
        /// </summary>
        /// <param name="reason">The reason for the disconnection.</param>
        void OnDisconnected(StopReason reason);
    }
}
