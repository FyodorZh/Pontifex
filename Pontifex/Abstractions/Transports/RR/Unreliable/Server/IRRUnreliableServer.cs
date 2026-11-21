using Pontifex.Utils;

namespace Pontifex.RR.Unreliable
{
    /// <summary>
    /// Server half of the RR unreliable contract: receives requests from clients and can send messages
    /// back to individual clients.
    /// </summary>
    public interface IRRUnreliableServer : ITransport
    {
        /// <summary>
        /// Inclusive maximum serialized size of one message, in bytes.
        /// </summary>
        int MessageMaxByteSize { get; }

        /// <summary>
        /// Binds the application handler before the transport is started.
        /// </summary>
        /// <param name="handler">The handler that receives the server callbacks.</param>
        /// <returns><see langword="true"/> if the handler was bound; otherwise <see langword="false"/>.</returns>
        bool Init(IRRUnreliableServerHandler handler);

        /// <summary>
        /// Sends <paramref name="message"/> to the client identified by <paramref name="client"/>.
        /// </summary>
        /// <param name="client">The route of the target client.</param>
        /// <param name="message">The message to send.</param>
        SendResult Send(IEndPoint client, UnionDataList message);
    }
}
