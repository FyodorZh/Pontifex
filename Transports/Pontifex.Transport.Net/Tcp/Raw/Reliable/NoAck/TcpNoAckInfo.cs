using System;

namespace Pontifex.Raw.Reliable.NoAck.Tcp
{
    /// <summary>
    /// Shared defaults and the canonical transport name for the reliable no-acknowledgement TCP carrier.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Other notes</b>: The values mirror the acknowledgement TCP carrier defaults, but
    /// the transport name is distinct: <c>tcp-noack</c>. Ack transport names, log tags, and stop reasons must not
    /// leak into no-acknowledgement diagnostics.</description></item>
    /// </list>
    /// </remarks>
    internal static class TcpNoAckInfo
    {
        public const string TransportName = "tcp-noack";
        public const int DefaultMessageMaxSize = 1024 * 1024;
        public const long DefaultBufferCapacity = 1024 * 1024 * 100;
        public const int ServerConnectionsLimit = 20000;
        public static readonly TimeSpan DefaultDisconnectTimeout = TimeSpan.FromSeconds(180);
    }
}
