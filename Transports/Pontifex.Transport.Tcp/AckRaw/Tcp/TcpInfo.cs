using Actuarius.Memory;
using Actuarius.PeriodicLogic;

namespace Pontifex.Transports.Tcp
{
    internal static class TcpInfo
    {
        public const string TransportName = "tcp";
        public const int MessageMaxByteSize = 1024 * 1023;
        public const int ServerConnectionsLimit = 20000;
        public static readonly DeltaTime DefaultDisconnectTimeout = DeltaTime.FromSeconds(180);

        public static readonly IMultiRefReadOnlyByteArray AckRequest = new StaticReadOnlyByteArray(System.Text.Encoding.UTF8.GetBytes("AckTcp"));
        public static readonly IMultiRefReadOnlyByteArray AckOKResponse = new StaticReadOnlyByteArray(System.Text.Encoding.UTF8.GetBytes("AckTcp-OK"));
    }
}
