namespace Pontifex.Raw.Reliable.Direct
{
    /// <summary>
    /// Shared defaults and the canonical names for the reliable Direct carrier.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Other notes</b>: <see cref="TransportName"/> is the binding scheme <c>direct</c>;
    /// <see cref="HandshakeName"/> is the transport handshake name and is also <c>direct</c>. The values mirror
    /// the TCP carrier.</description></item>
    /// </list>
    /// </remarks>
    internal static class RawReliableDirectInfo
    {
        public const string TransportName = "direct";
        public const string HandshakeName = "direct";
        public const int DefaultHandshakeMaxByteSize = 128;
    }
}
