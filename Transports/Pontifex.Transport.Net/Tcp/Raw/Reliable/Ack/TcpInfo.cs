using System;

namespace Pontifex.Raw.Reliable.Ack.Tcp
{
    internal static class TcpInfo
    {
        public const string TransportName = "tcp";
        public const int DefaultMessageMaxSize = 1024 * 1024;
        public const long DefaultBufferCapacity = 1024 * 1024 * 100;
        public const int ServerConnectionsLimit = 20000;
        public static readonly TimeSpan DefaultDisconnectTimeout = TimeSpan.FromSeconds(180);
    }
}
