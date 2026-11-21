using Pontifex.Utils;

namespace Pontifex.RR
{
    /// <summary>
    /// Base class of an RR server-side response handle.
    /// </summary>
    public abstract class RRResponse : IRRResponse
    {
        /// <summary>
        /// Initializes the handle with its immutable server-global request id.
        /// </summary>
        /// <param name="requestId">The server-global id of the request.</param>
        protected RRResponse(long requestId)
        {
            RequestId = requestId;
        }

        /// <inheritdoc/>
        public long RequestId { get; }

        /// <inheritdoc/>
        public abstract SendResult Response(UnionDataList response);
    }
}
