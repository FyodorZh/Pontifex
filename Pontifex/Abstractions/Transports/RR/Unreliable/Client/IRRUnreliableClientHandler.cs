using Pontifex.Utils;

namespace Pontifex.RR.Unreliable
{
    /// <summary>
    /// Application callbacks for one RR unreliable client.
    /// </summary>
    public interface IRRUnreliableClientHandler : IRRUnreliableHandler
    {
        /// <summary>
        /// Called for a response whose id matches a request sent by this client.
        /// </summary>
        /// <param name="requestId">The id returned by <see cref="IRRUnreliableClientTransport.Request"/>.</param>
        /// <param name="response">The received response. The handler owns the reference.</param>
        void OnResponded(long requestId, UnionDataList response);
    }
}
