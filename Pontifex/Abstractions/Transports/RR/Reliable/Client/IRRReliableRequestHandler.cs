using Pontifex.Utils;

namespace Pontifex.RR.Reliable
{
    /// <summary>
    /// Per-request client callback of the RR reliable contract.
    /// </summary>
    /// <remarks>
    /// Callbacks for one handler are serialized. Different requests may complete concurrently. A
    /// completed handler may be reused for a new request. Derive from <see cref="RRRequestHandler"/>,
    /// which supplies <see cref="IRRRequestHandler.RequestId"/> and
    /// <see cref="IRRRequestHandler.Cancel"/>.
    /// </remarks>
    public interface IRRReliableRequestHandler : IRRRequestHandler
    {
        /// <summary>
        /// Called for a response of the request. Multiple responses are allowed before
        /// <see cref="Finish"/>.
        /// </summary>
        /// <param name="response">The received response. The handler owns the reference.</param>
        void Response(UnionDataList response);

        /// <summary>
        /// Called exactly once when the request reaches its terminal state. No
        /// <see cref="Response"/> call follows it.
        /// </summary>
        /// <param name="type">How the request completed.</param>
        void Finish(RRReliableCompletionType type);
    }
}
