namespace Pontifex.Raw.Reliable
{
    /// <summary>
    /// Framing constants shared by the reliable carriers that prepend a frame type and a length prefix.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Other notes</b>: <see cref="MessageMaxByteSize"/> limits the application payload
    /// only. A framed receive buffer must also admit the transport framing, so a payload exactly at the limit
    /// is not rejected by a carrier receive cap.</description></item>
    /// </list>
    /// </remarks>
    public static class RawReliableFraming
    {
        /// <summary>
        /// The maximum number of bytes that framing can add to one payload: the frame-type element and the
        /// maximum growth of the list-count varint, with a conservative margin for the length prefix.
        /// </summary>
        public const int MaxFrameOverheadBytes = 16;

        /// <summary>
        /// Computes the framed receive cap for a payload limit.
        /// </summary>
        /// <param name="messageMaxByteSize">The inclusive application-payload limit.</param>
        /// <returns>The saturated framed byte cap.</returns>
        public static int FramedMaxByteSize(int messageMaxByteSize)
        {
            long framed = (long)messageMaxByteSize + MaxFrameOverheadBytes;
            return framed > int.MaxValue ? int.MaxValue : (int)framed;
        }
    }
}
