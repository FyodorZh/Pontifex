namespace Pontifex.RR.Reliable
{
    /// <summary>
    /// Server-side handle of one reliable request presented to
    /// <see cref="IRRReliableServerHandler.OnRequested"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A handle starts undecided. The first successful <see cref="Response"/> or <see cref="Finish"/>
    /// call accepts the request; a successful <see cref="Reject"/> call rejects it. An undecided
    /// handle stays usable after <see cref="IRRReliableServerHandler.OnRequested"/> returns and may
    /// also be used from inside that call, so a handler may answer synchronously or asynchronously.
    /// </para>
    /// <para>
    /// Calls may be made from any thread; the transport serializes them. A call made after the
    /// handle is closed — by a successful <see cref="Finish"/>, by
    /// <see cref="IRRServerTransport.ResponseTtl"/> expiry, or by a transport stop — returns
    /// <see cref="SendResult.Error"/>. After <see cref="Reject"/> succeeded,
    /// <see cref="IRRResponse.Response"/> and <see cref="Finish"/> throw
    /// <see cref="System.InvalidOperationException"/> instead.
    /// </para>
    /// </remarks>
    public interface IRRReliableResponse : IRRResponse
    {
        /// <summary>
        /// Completes the request normally and accepts the request if it is still undecided. After a
        /// successful call no further <see cref="Response"/> call is allowed.
        /// </summary>
        /// <returns><see cref="SendResult.Ok"/> for the call that completes the request; otherwise
        /// <see cref="SendResult.Error"/> when the handle is already completed or closed.</returns>
        /// <exception cref="System.InvalidOperationException">The request was rejected with
        /// <see cref="Reject"/>.</exception>
        SendResult Finish();

        /// <summary>
        /// Rejects the request.
        /// </summary>
        /// <remarks>
        /// The rejection is delivered to the client on a best-effort basis. If it does not arrive
        /// before the client request TTL expires, the client completes the request with
        /// <see cref="RRReliableCompletionType.Failed"/> instead of
        /// <see cref="RRReliableCompletionType.Rejected"/>.
        /// </remarks>
        /// <returns><see cref="SendResult.Ok"/> when the request is rejected, including a repeated
        /// call; <see cref="SendResult.Error"/> when the transport is stopping or stopped.</returns>
        /// <exception cref="System.InvalidOperationException">The request was already accepted by
        /// <see cref="Response"/> or <see cref="Finish"/>.</exception>
        SendResult Reject();
    }
}
