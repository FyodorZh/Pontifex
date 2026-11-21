using Pontifex.Utils;

namespace Pontifex.RR.Unreliable
{
    /// <summary>
    /// Client-side handle for sending messages to the server.
    /// </summary>
    public interface IRRUnreliableServerEndpoint
    {
        /// <summary>
        /// The route of the server this endpoint sends to.
        /// </summary>
        IEndPoint EndPoint { get; }

        /// <summary>
        /// Inclusive maximum serialized size of one message, in bytes.
        /// </summary>
        int MessageMaxByteSize { get; }

        /// <summary>
        /// Sends <paramref name="message"/> to the server.
        /// </summary>
        /// <param name="message">The message to send.</param>
        SendResult Send(UnionDataList message);
    }
}
