using System;

namespace Pontifex.RR
{
    /// <summary>
    /// Server half shared by the RR families: every response stream is bounded by a TTL.
    /// </summary>
    public interface IRRServerTransport : IRRTransport
    {
        /// <summary>
        /// Gets the maximum time a response stream may stay undecided or open, counted from the moment
        /// the server received the request.
        /// </summary>
        TimeSpan ResponseTtl { get; }
    }
}
