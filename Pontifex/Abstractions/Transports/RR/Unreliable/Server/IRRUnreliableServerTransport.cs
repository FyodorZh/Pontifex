using Pontifex.Utils;

namespace Pontifex.RR.Unreliable
{
    /// <summary>
    /// Server half of the RR unreliable contract: receives requests from clients and sends the matched
    /// responses back to their sources.
    /// </summary>
    public interface IRRUnreliableServerTransport : IRRUnreliableTransport
    {
        /// <summary>
        /// Binds the application handler before the transport is started.
        /// </summary>
        /// <param name="handler">The handler that receives the request callbacks.</param>
        /// <returns><see langword="true"/> if the handler was bound; otherwise <see langword="false"/>.</returns>
        bool Init(IRRUnreliableServerHandler handler);

        /// <summary>
        /// Sends <paramref name="response"/> to the client that sent the request identified by
        /// <paramref name="requestId"/>.
        /// </summary>
        /// <param name="requestId">The server-global id delivered by
        /// <see cref="IRRUnreliableServerHandler.OnRequested"/>.</param>
        /// <param name="response">The response payload. The transport owns it after the call.</param>
        /// <returns>The result of the send attempt.</returns>
        SendResult Response(long requestId, UnionDataList response);
    }
}
