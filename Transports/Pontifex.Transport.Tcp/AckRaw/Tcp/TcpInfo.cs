using System;
using Actuarius.Memory;
using Operarius;

namespace Pontifex.Transports.Tcp
{
    internal static class TcpInfo
    {
        public const string TransportName = "tcp";
        public const int MessageMaxByteSize = 1024 * 1023;
        public const int ServerConnectionsLimit = 20000;
        public static readonly TimeSpan DefaultDisconnectTimeout = TimeSpan.FromSeconds(180);

        public static readonly IMultiRefReadOnlyByteArray AckRequest = new StaticReadOnlyByteArray(System.Text.Encoding.UTF8.GetBytes("AckTcp"));
        public static readonly IMultiRefReadOnlyByteArray AckOKResponse = new StaticReadOnlyByteArray(System.Text.Encoding.UTF8.GetBytes("AckTcp-OK"));
    }
}
