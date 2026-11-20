namespace Pontifex.Raw.Reliable.Direct
{
    /// <summary>
    /// Shared defaults and the canonical names for the reliable Direct carrier.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Other notes</b>: <see cref="TransportName"/> is the temporary binding scheme
    /// <c>direct-noack</c>; <see cref="HandshakeName"/> is the final contract name <c>direct</c>. The values
    /// mirror the TCP carrier: the binding scheme is temporary to avoid a duplicate <c>direct</c>
    /// registration, while the handshake name is stable.</description></item>
    /// </list>
    /// </remarks>
    internal static class RawReliableDirectInfo
    {
        public const string TransportName = "direct-noack";
        public const string HandshakeName = "direct";
        public const int DefaultHandshakeMaxByteSize = 128;
    }
}
