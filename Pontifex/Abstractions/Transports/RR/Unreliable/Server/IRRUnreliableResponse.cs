namespace Pontifex.RR.Unreliable
{
    /// <summary>
    /// Server-side handle of one unreliable request presented to
    /// <see cref="IRRUnreliableServerHandler.OnRequested"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The handle answers the request with a declared number of messages. The count defaults to one
    /// and may be set once, before the first <see cref="IRRResponse.Response"/> call, while the handle
    /// can answer. Each accepted send carries the next zero-based message id; the handle stops
    /// answering after the last message.
    /// </para>
    /// <para>
    /// The handle stays usable after <see cref="IRRUnreliableServerHandler.OnRequested"/> returns and
    /// may also be used from inside that call. A call made after the handle can no longer answer — by
    /// its last message, a rejection, <see cref="IRRServerTransport.ResponseTtl"/> expiry, or a
    /// transport stop — returns <see cref="SendResult.Error"/> and closes the handle.
    /// </para>
    /// </remarks>
    public interface IRRUnreliableResponse : IRRResponse
    {
        /// <summary>
        /// Gets the number of messages this response consists of. Defaults to <c>1</c>.
        /// </summary>
        int MessageCount { get; }

        /// <summary>
        /// Sets the number of messages this response consists of.
        /// </summary>
        /// <param name="messageCount">The message count, from <c>1</c> to <c>256</c>.</param>
        /// <remarks>
        /// The range is validated first: a value outside <c>1..256</c> throws
        /// <see cref="System.ArgumentOutOfRangeException"/> even on a closed handle. The count may be
        /// set once, before the first <see cref="IRRResponse.Response"/> call, while the handle can
        /// answer.
        /// </remarks>
        /// <exception cref="System.ArgumentOutOfRangeException"><paramref name="messageCount"/> is
        /// outside the range <c>1..256</c>.</exception>
        /// <exception cref="System.InvalidOperationException">The count was already set, the first
        /// <see cref="IRRResponse.Response"/> call was already made, the handle is closed, the
        /// response TTL expired, or the transport stopped.
        /// </exception>
        void SetMessageCount(int messageCount);
    }
}
