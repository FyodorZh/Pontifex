using Pontifex.Utils;

namespace Pontifex.Ack.Raw
{
    /// <summary>
    /// Represents one side of a raw ACK-based connection.
    /// Provides send/disconnect capabilities and metadata about the remote peer.
    /// Implemented by the transport system; consumed by business logic.
    /// </summary>
    public interface IAckRawReliableBaseEndpoint : IAckRawBaseEndpoint
    {
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
    }
}