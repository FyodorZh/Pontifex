using Pontifex.Utils;

namespace Pontifex.RR.Unreliable
{
    /// <summary>
    /// Application callbacks for one RR unreliable server.
    /// </summary>
    public interface IRRUnreliableServerHandler : IRRUnreliableHandler
    {
        /// <summary>
        /// Called for every request received from a client.
        /// </summary>
        /// <param name="requestId">The server-global id that identifies the request, across all
        /// clients.</param>
        /// <param name="client">The route of the client that sent the request.</param>
        /// <param name="request">The received request. The handler owns the reference.</param>
        /// <returns><see langword="true"/> if the request is accepted; <see langword="false"/> if it is
        /// rejected. A rejected request cannot be answered: the transport drops its pending-response
        /// route, so a later <see cref="IRRUnreliableServerTransport.Response"/> with this id fails with
        /// <see cref="SendResult.InvalidAddress"/>.</returns>
        bool OnRequested(long requestId, IEndPoint client, UnionDataList request);
    }
}
