using System;
using Pontifex.Utils;

namespace Pontifex.NoAck.Raw
{
    public interface INoAckRawUnreliableClient : ITransport
    {
        event Action<UnionDataList>? OnReceived;
        
        /// <summary>
        /// Maximum allowed size of a single message for sending (and receiving)
        /// </summary>
        int MessageMaxByteSize { get; }

        /// <summary>
        /// Attempts to send a message to the server. Returns a SendResult indicating success or failure.
        /// </summary>
        /// <param name="message">The message to send.</param>
        /// <returns>A SendResult indicating the outcome of the send operation.</returns>
        SendResult TrySend(UnionDataList message);
    }
}