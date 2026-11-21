namespace Pontifex.RR.Unreliable
{
    /// <summary>
    /// Server-side handle of one unreliable request presented to
    /// <see cref="IRRUnreliableServerHandler.OnRequested"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The handle answers the request at most once. It stays usable after
    /// <see cref="IRRUnreliableServerHandler.OnRequested"/> returns and may also be used from inside
    /// that call. The first successful <see cref="Response"/> call closes the handle before the send
    /// is attempted, so a stale handle never answers twice.
    /// </para>
    /// <para>
    /// Calls may be made from any thread; the transport serializes them. A call made after the
    /// handle is closed — by <see cref="IRRResponse.Response"/>, by
    /// <see cref="IRRServerTransport.ResponseTtl"/> expiry, by a rejection, or by a transport stop —
    /// returns <see cref="SendResult.Error"/>.
    /// </para>
    /// </remarks>
    public interface IRRUnreliableResponse : IRRResponse
    {
    }
}
