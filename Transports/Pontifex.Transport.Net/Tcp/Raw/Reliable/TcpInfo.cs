using System;

namespace Pontifex.Raw.Reliable.Tcp
{
    /// <summary>
    /// Shared defaults and the canonical transport name for the reliable TCP carrier.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Other notes</b>: <see cref="TransportName"/> is the binding scheme <c>tcp</c>;
    /// <see cref="HandshakeName"/> is the transport handshake name and is also <c>tcp</c>. Transport names, log
    /// tags, and stop reasons must not leak into reliable-transport diagnostics.</description></item>
    /// </list>
    /// </remarks>
    internal static class TcpInfo
    {
        public const string TransportName = "tcp";
        public const string HandshakeName = "tcp";
        public const int DefaultMessageMaxSize = 1024 * 1024;
        public const long DefaultBufferCapacity = 1024 * 1024 * 100;
        public const int ServerConnectionsLimit = 20000;
        public static readonly TimeSpan DefaultDisconnectTimeout = TimeSpan.FromSeconds(180);
    }
}
