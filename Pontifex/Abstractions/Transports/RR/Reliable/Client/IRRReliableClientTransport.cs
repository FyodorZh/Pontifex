using Pontifex.Utils;

namespace Pontifex.RR.Reliable
{
    /// <summary>
    /// Client half of the RR reliable contract: sends requests to a server, receives zero or more
    /// responses for each request, and observes the terminal completion of every accepted request.
    /// </summary>
    public interface IRRReliableClientTransport : IRRReliableTransport
    {
        /// <summary>
        /// Prepares the client before it is started.
        /// </summary>
        /// <returns><see langword="true"/> if the client was initialized; otherwise
        /// <see langword="false"/>.</returns>
        bool Init();

        /// <summary>
        /// Sends <paramref name="request"/> to the server and binds <paramref name="handler"/> to it.
        /// </summary>
        /// <param name="request">The request payload. The transport owns it after the call.</param>
        /// <param name="handler">The handler of this request. It must not already be bound to another
        /// pending request.</param>
        /// <returns>The result of the send attempt.</returns>
        /// <exception cref="System.ArgumentNullException"><paramref name="handler"/> is
        /// <see langword="null"/>.</exception>
        /// <exception cref="System.InvalidOperationException"><paramref name="handler"/> is already
        /// bound to a pending request.</exception>
        /// <remarks>
        /// A request accepted by this call receives exactly one
        /// <see cref="IRRReliableRequestHandler.Finish"/> call. A request that is rejected by this
        /// call receives no handler callbacks.
        /// </remarks>
        SendResult Request(UnionDataList request, IRRReliableRequestHandler handler);

        /// <summary>
        /// Cancels the request bound to <paramref name="handler"/>.
        /// </summary>
        /// <param name="handler">The handler passed to a still-pending
        /// <see cref="Request"/> call.</param>
        /// <exception cref="System.ArgumentNullException"><paramref name="handler"/> is
        /// <see langword="null"/>.</exception>
        /// <remarks>
        /// The call is idempotent. It reports
        /// <see cref="RRReliableCompletionType.Canceled"/> through
        /// <see cref="IRRReliableRequestHandler.Finish"/> and suppresses every later response callback
        /// for that request. A completed or unknown handler is a no-op.
        /// </remarks>
        void CancelRequest(IRRReliableRequestHandler handler);
    }
}
