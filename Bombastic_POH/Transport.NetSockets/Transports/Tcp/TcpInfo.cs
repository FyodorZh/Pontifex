namespace Transport.Transports.Tcp
{
    internal static class TcpInfo
    {
        public const string TransportName = "tcp";
        public const int MessageMaxByteSize = 1024 * 64;
        public const int ServerConnectionsLimit = 20000;
        public static readonly Shared.DeltaTime DefaultDisconnectTimeout = Shared.DeltaTime.FromSeconds(180);

        public static readonly byte[] AckRequest = System.Text.Encoding.UTF8.GetBytes("AckTcp");
        public static readonly byte[] AckOKResponse = System.Text.Encoding.UTF8.GetBytes("AckTcp-OK");
    }
}
