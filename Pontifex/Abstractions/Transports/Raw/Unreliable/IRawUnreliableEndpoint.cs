using Pontifex.Utils;

namespace Pontifex.Raw.Unreliable
{
    public interface IRawUnreliableEndpoint : IRawEndpoint
    {
        bool IsValid { get; }
        
        /// <summary>
        /// Attempts to send a message to a remote endpoint. Returns a SendResult indicating success or failure.
        /// </summary>
        /// <param name="message">The message to send.
        /// Ownership transfers to the transport for every non-null message argument, regardless of the result.</param>
        /// <returns>A SendResult indicating the outcome of the send operation.
        /// Success indicates that this transport did the best effort to deliver the message to the remote endpoint,
        /// but actual delivery is not verifiable. All kinds of corruptions are possible: Loss, Reorder, Duplication</returns>
        SendResult UnreliableSend(UnionDataList message);

        void Stop(StopReason reason);
    }
}