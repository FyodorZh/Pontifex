using Pontifex.Utils;

namespace Pontifex.Raw.Unreliable.Ack
{
    /// <summary>
    /// Represents one side of a raw ACK-based connection.
    /// Provides send/disconnect capabilities and metadata about the remote peer.
    /// Implemented by the transport system; consumed by business logic.
    /// </summary>
    public interface IRawUnreliableAckBaseEndpoint : IRawAckBaseEndpoint
    {
        /// <summary>
        /// Sends a message to the remote peer.
        /// No delivery guarantee is provided; the message may be lost or arrive out of order or duplicated.
        /// </summary>
        /// <param name="bufferToSend">The data to send.</param>
        /// <returns>SendResult.Ok on success; other values indicate a synchronous failure.</returns>
        SendResult TrySend(UnionDataList bufferToSend);
    }
}