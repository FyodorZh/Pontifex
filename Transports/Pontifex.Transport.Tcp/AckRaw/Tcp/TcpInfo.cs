using Actuarius.PeriodicLogic;

namespace Pontifex.Transports.Tcp
{
    internal static class TcpInfo
    {
        public const string TransportName = "tcp";
        public const int MessageMaxByteSize = 1024 * 64;
        public const int ServerConnectionsLimit = 20000;
        public static readonly DeltaTime DefaultDisconnectTimeout = DeltaTime.FromSeconds(180);

        public static readonly string AckRequest = "AckTcp";
        public static readonly byte[] AckOKResponse = System.Text.Encoding.UTF8.GetBytes("AckTcp-OK");
    }
}
