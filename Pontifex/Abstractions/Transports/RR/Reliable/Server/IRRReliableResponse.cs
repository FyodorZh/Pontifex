using Pontifex.Utils;

namespace Pontifex.RR.Reliable
{
    /// <summary>
    /// Server-side handle of one accepted RR reliable request.
    /// </summary>
    /// <remarks>
    /// The handle stays usable after <see cref="IRRReliableServerHandler.OnRequested"/> returns. Calls
    /// may be made concurrently; the transport serializes them. After <see cref="Finish"/>, a
    /// cancellation, or a transport stop, <see cref="Response"/> and <see cref="Finish"/> return
    /// <see cref="SendResult.Error"/>.
    /// </remarks>
    public interface IRRReliableResponse
    {
        /// <summary>
        /// Gets the server-global id of the request, unique across all clients.
        /// </summary>
        long RequestId { get; }

        /// <summary>
        /// Sends one response for the request. Multiple responses are allowed before
        /// <see cref="Finish"/>.
        /// </summary>
        /// <param name="response">The response payload. The transport owns it after the call.</param>
        /// <returns>The result of the send attempt.</returns>
        SendResult Response(UnionDataList response);

        /// <summary>
        /// Completes the request normally. After a successful call no further <see cref="Response"/>
        /// calls are allowed.
        /// </summary>
        /// <returns><see cref="SendResult.Ok"/> for the call that completes the request; otherwise
        /// <see cref="SendResult.Error"/>.</returns>
        SendResult Finish();
    }
}
