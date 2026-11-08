namespace Pontifex.Raw.Reliable.Ack
{
    /// <summary>
    /// Shared protocol limits for the acknowledgement handshake.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Other notes</b>: The limits are part of the wire contract and must agree on
    /// both peers.</description></item>
    /// </list>
    /// </remarks>
    public static class RawReliableAckInfo
    {
        /// <summary>
        /// The inclusive maximum size, in bytes, of the serialized acknowledgement request and response
        /// payloads. The size excludes the four-byte frame-length prefix.
        /// </summary>
        public const int AckMaxByteSize = 4096;
    }
}
