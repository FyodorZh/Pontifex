using System;
using Pontifex.Utils;

namespace Pontifex.Raw.Reliable.NoAck
{
    public interface IRawReliableNoAckClient : ITransport
    {
        event Action<UnionDataList>? OnReceived;
        
        /// <summary>
        /// Maximum allowed size of a single message for sending (and receiving)
        /// </summary>
        int MessageMaxByteSize { get; }

        /// <summary>
        /// Send a message to the server. Returns a SendResult indicating success or failure.
        /// </summary>
        /// <param name="message">The message to send.</param>
        /// <returns>A SendResult indicating the outcome of the send operation.</returns>
        SendResult Send(UnionDataList message);
    }
}