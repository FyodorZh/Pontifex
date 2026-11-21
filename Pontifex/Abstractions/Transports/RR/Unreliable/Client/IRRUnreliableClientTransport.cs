using Pontifex.Utils;

namespace Pontifex.RR.Unreliable
{
    /// <summary>
    /// Client half of the RR unreliable contract: sends requests to a fixed server and receives at most
    /// one response per request through the request's own handler.
    /// </summary>
    /// <remarks>
    /// The client binds no transport-level handler and has no initialization step; it is ready for
    /// <see cref="ITransport.Start"/> after construction.
    /// </remarks>
    public interface IRRUnreliableClientTransport : IRRClientTransport
    {
        /// <summary>
        /// Sends <paramref name="request"/> to the server and binds <paramref name="handler"/> to it.
        /// </summary>
        /// <param name="request">The request payload. The transport owns it after the call.</param>
        /// <param name="handler">The handler of this request. It must derive from
        /// <see cref="RRRequestHandler"/> and must not already be bound to another pending
        /// request.</param>
        /// <returns>The result of the send attempt. On <see cref="SendResult.Ok"/>,
        /// <see cref="IRRRequestHandler.RequestId"/> is the client-local id of the request.</returns>
        /// <exception cref="System.ArgumentNullException"><paramref name="handler"/> is
        /// <see langword="null"/>.</exception>
        /// <exception cref="System.ArgumentException"><paramref name="handler"/> does not derive from
        /// <see cref="RRRequestHandler"/>.</exception>
        /// <exception cref="System.InvalidOperationException"><paramref name="handler"/> is already
        /// bound to a pending request.</exception>
        SendResult Request(UnionDataList request, IRRUnreliableRequestHandler handler);
    }
}
