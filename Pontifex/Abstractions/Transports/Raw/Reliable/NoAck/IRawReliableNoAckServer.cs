using System;
using Pontifex.Utils;

namespace Pontifex.Raw.Reliable.NoAck
{
    public interface IRawReliableNoAckServer : ITransport
    {
        /// <summary>
        /// Event triggered when a message is received from a client.
        /// The event provides the source endpoint and the received message.
        /// </summary>
        event Action<IEndPoint, UnionDataList>? OnReceived;
        
        /// <summary>
        /// Maximum allowed size of a single message for sending (and receiving)
        /// </summary>
        int MessageMaxByteSize { get; }
        
        /// <summary>
        /// Send a message to the specified destination. Returns a SendResult indicating success or failure.
        /// </summary>
        /// <param name="destination">The destination endpoint to send the message to.</param>
        /// <param name="message">The message to send.</param>
        /// <returns>A SendResult indicating the outcome of the send operation.</returns>
        SendResult Send(IEndPoint destination, UnionDataList message);
    }
}