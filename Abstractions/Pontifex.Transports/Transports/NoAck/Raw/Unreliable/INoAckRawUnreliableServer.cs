using System;
using Pontifex.Utils;

namespace Pontifex.NoAck.Raw
{
    public interface INoAckRawUnreliableServer : ITransport
    {
        /// <summary>
        /// Event triggered when a message is received from one of the clients.
        /// - The event handler should not perform any blocking operations or long-running tasks to avoid blocking the transport's internal processing.
        /// - The event handler should also not throw exceptions, as this could disrupt the transport's internal processing.
        /// - The event handler may assume that all invocations are made strictly sequentially,
        ///   meaning that the next invocation will not occur until the previous one has completed.
        /// </summary>
        event Action<IEndPoint, UnionDataList>? OnReceived;
        
        /// <summary>
        /// Maximum allowed size of a single message for sending (and receiving)
        /// </summary>
        int MessageMaxByteSize { get; }
        
        /// <summary>
        /// Attempts to send a message to the specified destination. Returns a SendResult indicating success or failure.
        /// </summary>
        /// <param name="destination">The destination endpoint to send the message to.</param>
        /// <param name="message">The message to send. Sender owns the message and is responsible for its lifecycle.</param>
        /// <returns>A SendResult indicating the outcome of the send operation.</returns>
        SendResult TrySend(IEndPoint destination, UnionDataList message);
    }
}