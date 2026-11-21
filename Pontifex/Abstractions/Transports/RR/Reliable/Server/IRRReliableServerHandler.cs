using Pontifex.Utils;

namespace Pontifex.RR.Reliable
{
    /// <summary>
    /// Application callbacks for one RR reliable server.
    /// </summary>
    public interface IRRReliableServerHandler : IHandler
    {
        /// <summary>
        /// Called once for every request the server presents.
        /// </summary>
        /// <param name="client">The route of the client that sent the request.</param>
        /// <param name="request">The received request. The handler owns the reference and must
        /// release it exactly once.</param>
        /// <param name="response">The handle used to answer or reject the request. It starts
        /// undecided and may be used inside this call or later.</param>
        /// <remarks>
        /// The handler decides the outcome through the handle:
        /// <see cref="IRRReliableResponse.Response"/> or <see cref="IRRReliableResponse.Finish"/>
        /// accepts the request, and <see cref="IRRReliableResponse.Reject"/> rejects it. Returning
        /// without deciding leaves the handle live until it is answered or its TTL expires.
        /// A throwing handler is logged; an undecided request is rejected, while an already accepted
        /// or rejected request keeps its state.
        /// </remarks>
        void OnRequested(IEndPoint client, UnionDataList request, IRRReliableResponse response);
    }
}
