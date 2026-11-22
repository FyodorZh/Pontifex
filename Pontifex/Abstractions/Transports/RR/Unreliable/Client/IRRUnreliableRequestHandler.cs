using Pontifex.Utils;

namespace Pontifex.RR.Unreliable
{
    /// <summary>
    /// Per-request client callback of the RR unreliable contract.
    /// </summary>
    /// <remarks>
    /// A request receives at most one <see cref="Response"/> call and no completion callback. Derive
    /// from <see cref="RRRequestHandler"/>, which supplies <see cref="IRRRequestHandler.RequestId"/>
    /// and <see cref="IRRRequestHandler.Cancel"/>.
    /// </remarks>
    public interface IRRUnreliableRequestHandler : IRRRequestHandler
    {
        /// <summary>
        /// Called at most once, with the first response whose id matches the bound request and whose
        /// source is the configured server route.
        /// </summary>
        /// <param name="response">The received response. The handler owns the reference.</param>
        void Response(UnionDataList response);
    }
}
