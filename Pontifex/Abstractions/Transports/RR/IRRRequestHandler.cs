namespace Pontifex.RR
{
    /// <summary>
    /// Common per-request client handler of the RR family.
    /// </summary>
    /// <remarks>
    /// The transport binds the handler to one request. While the handler is bound,
    /// <see cref="RequestId"/> is the client-local id of that request and <see cref="Cancel"/>
    /// cancels it. Both members are supplied by <see cref="RRRequestHandler"/>; a transport rejects
    /// a handler that does not derive from it.
    /// </remarks>
    public interface IRRRequestHandler : IHandler
    {
        /// <summary>
        /// Gets the client-local id of the request the handler is bound to, or the id of the request
        /// it was last bound to. The value is <c>-1</c> before the first successful binding.
        /// </summary>
        long RequestId { get; }

        /// <summary>
        /// Cancels the request currently bound to this handler, if any.
        /// </summary>
        /// <remarks>
        /// The call is idempotent and thread-safe. A handler that is not bound to a pending request is
        /// a no-op. The observable effect is defined by the family: the reliable family delivers a
        /// terminal <c>Finish</c> with the <c>Canceled</c> completion type and may inform the server;
        /// the unreliable family silently stops accepting a response for the request.
        /// </remarks>
        void Cancel();
    }
}
