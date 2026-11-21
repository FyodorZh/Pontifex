using Pontifex.Utils;

namespace Pontifex.RR.Reliable
{
    /// <summary>
    /// Application callbacks for one RR reliable server.
    /// </summary>
    public interface IRRReliableServerHandler : IHandler
    {
        /// <summary>
        /// Called for every request received from a client.
        /// </summary>
        /// <param name="client">The route of the client that sent the request.</param>
        /// <param name="request">The received request. The handler owns the reference.</param>
        /// <param name="response">The handle used to answer the request. Its
        /// <see cref="IRRReliableResponse.RequestId"/> is the server-global request id.</param>
        /// <returns><see langword="true"/> if the request is accepted; <see langword="false"/> if it is
        /// rejected. A rejected request completes on the client with
        /// <see cref="RRReliableCompletionType.Rejected"/> and cannot be answered. A throwing handler
        /// is logged and treated as a rejection.</returns>
        bool OnRequested(IEndPoint client, UnionDataList request, IRRReliableResponse response);
    }
}
