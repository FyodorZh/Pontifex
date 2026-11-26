using Pontifex.Utils;

namespace Pontifex.RR.Unreliable
{
    /// <summary>
    /// Application callbacks for one RR unreliable server.
    /// </summary>
    public interface IRRUnreliableServerHandler : IHandler
    {
        /// <summary>
        /// Called for every request received from a client.
        /// </summary>
        /// <param name="client">The route of the client that sent the request.</param>
        /// <param name="request">The received request. The handler owns the reference and must
        /// release it exactly once.</param>
        /// <param name="response">The handle used to answer the request. Its
        /// <see cref="IRRResponse.RequestId"/> is the server-global request id and its
        /// <see cref="IRRUnreliableResponse.MessageCount"/> defaults to one.</param>
        /// <returns><see langword="true"/> if the request is accepted; <see langword="false"/> if it
        /// is rejected. A rejected request closes the handle, so a later
        /// <see cref="IRRUnreliableResponse.Response"/> returns <see cref="SendResult.Error"/>.
        /// A throwing handler is logged and treated as a rejection.</returns>
        bool OnRequested(IEndPoint client, UnionDataList request,
            IRRUnreliableResponse response);
    }
}
