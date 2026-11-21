using Pontifex.Utils;

namespace Pontifex.RR.Reliable
{
    /// <summary>
    /// Client half of the RR reliable contract: sends requests to a fixed server, receives zero or more
    /// responses for each request, and observes the terminal completion of every accepted request.
    /// </summary>
    /// <remarks>
    /// The client binds no transport-level handler and has no initialization step; it is ready for
    /// <see cref="ITransport.Start"/> after construction. Each request carries its own
    /// <see cref="IRRReliableRequestHandler"/>.
    /// </remarks>
    public interface IRRReliableClientTransport : IRRClientTransport
    {
        /// <summary>
        /// Sends <paramref name="request"/> to the server and binds <paramref name="handler"/> to it.
        /// </summary>
        /// <param name="request">The request payload. The transport owns it after the call.</param>
        /// <param name="handler">The handler of this request. It must derive from
        /// <see cref="RRRequestHandler"/> and must not already be bound to another pending
        /// request.</param>
        /// <returns>The result of the send attempt.</returns>
        /// <exception cref="System.ArgumentNullException"><paramref name="handler"/> is
        /// <see langword="null"/>.</exception>
        /// <exception cref="System.ArgumentException"><paramref name="handler"/> does not derive from
        /// <see cref="RRRequestHandler"/>.</exception>
        /// <exception cref="System.InvalidOperationException"><paramref name="handler"/> is already
        /// bound to a pending request.</exception>
        /// <remarks>
        /// A request accepted by this call receives exactly one
        /// <see cref="IRRReliableRequestHandler.Finish"/> call. A request that is rejected by this
        /// call receives no handler callbacks.
        /// </remarks>
        SendResult Request(UnionDataList request, IRRReliableRequestHandler handler);
    }
}
