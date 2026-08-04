using Pontifex.Utils;

namespace Pontifex.Raw.Reliable
{
    public interface IRawReliableEndpoint : IRawEndpoint
    {
        /// <summary>
        /// Gets whether the endpoint is currently connected.
        /// May return true before OnConnected() is invoked (the endpoint reference
        /// is not available to the caller before OnConnected()).
        /// Guaranteed to return false during and after OnDisconnected() — the
        /// IsConnected transition to false is synchronized with the OnDisconnected() call.
        /// </summary>
        bool IsConnected { get; }
        
        /// <summary>
        /// Sends a message to the remote peer.
        /// Synchronous failures (e.g., buffer full, message too big, not connected)
        /// are returned as a non-Ok SendResult and do NOT affect the connection.
        /// If a failure occurs asynchronously after the method returns Ok,
        /// the transport will be destroyed and OnDisconnected will be raised.
        /// </summary>
        /// <param name="bufferToSend">The data to send.</param>
        /// <returns>SendResult.Ok on success; other values indicate a synchronous failure.</returns>
        SendResult Send(UnionDataList bufferToSend);
        
        /// <summary>
        /// Initiates a logical disconnection of this endpoint with the given reason.
        /// </summary>
        /// <param name="reason">The reason for the disconnection.</param>
        /// <returns>True if the disconnect was initiated successfully.</returns>
        bool Disconnect(StopReason reason);
    }
}