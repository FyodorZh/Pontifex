using Pontifex.Utils;

namespace Pontifex.RR.Unreliable
{
    /// <summary>
    /// Per-request client callback of the RR unreliable contract.
    /// </summary>
    /// <remarks>
    /// A request receives zero or more <see cref="Response"/> calls and no completion callback. The
    /// server declares the number of messages up front; each call carries the zero-based id of its
    /// message and the total count. A message may be lost, duplicated, or reordered, and the last
    /// message may never arrive. Derive from <see cref="RRRequestHandler"/>, which supplies
    /// <see cref="IRRRequestHandler.RequestId"/> and <see cref="IRRRequestHandler.Cancel"/>.
    /// </remarks>
    public interface IRRUnreliableRequestHandler : IRRRequestHandler
    {
        /// <summary>
        /// Called once per received response message, with a message whose request id matches the
        /// bound request and whose source is the configured server route.
        /// </summary>
        /// <param name="response">The received message. The handler owns the reference.</param>
        /// <param name="messageId">The zero-based id of the message, from <c>0</c> to
        /// <paramref name="messageCount"/> minus one.</param>
        /// <param name="messageCount">The number of messages the server declared for this response,
        /// from <c>1</c> to <c>256</c>.</param>
        void Response(UnionDataList response, int messageId, int messageCount);
    }
}
