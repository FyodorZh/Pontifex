using Pontifex.Utils;

namespace Pontifex.RR
{
    /// <summary>
    /// Common server-side handle of one RR request.
    /// </summary>
    public interface IRRResponse
    {
        /// <summary>
        /// Gets the server-global id of the request, unique across all clients.
        /// </summary>
        long RequestId { get; }

        /// <summary>
        /// Sends one response for the request.
        /// </summary>
        /// <param name="response">The response payload. The transport owns it after a non-throwing
        /// call.</param>
        /// <returns>The result of the send attempt. <see cref="SendResult.Ok"/> is local acceptance,
        /// not client receipt.</returns>
        SendResult Response(UnionDataList response);
    }
}
