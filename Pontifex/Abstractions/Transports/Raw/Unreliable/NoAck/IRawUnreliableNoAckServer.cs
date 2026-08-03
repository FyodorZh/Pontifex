using System;
using Pontifex.Utils;

namespace Pontifex.Raw.Unreliable.NoAck
{
    public interface IRawUnreliableNoAckServer : ITransport
    {
        /// <summary>
        /// Event triggered when a message is received from one of the clients.
        /// - The event handler should not perform any blocking operations or long-running tasks to avoid blocking the transport's internal processing.
        /// - The transport catches handler exceptions and continues processing later messages.
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
        /// <param name="message">The message to send. Ownership transfers to the transport for every non-null call result.</param>
        /// <returns>A SendResult indicating the outcome of the send operation.</returns>
        SendResult TrySend(IEndPoint destination, UnionDataList message);
    }
}
