using System;
using Actuarius.Memory;

namespace Pontifex.Raw.Reliable.Ack.Tcp
{
    internal static class TcpInfo
    {
        public const string TransportName = "tcp";
        public const int DefaultMessageMaxSize = 1024 * 1023 * 100;
        public const int ServerConnectionsLimit = 20000;
        public static readonly TimeSpan DefaultDisconnectTimeout = TimeSpan.FromSeconds(180);

        public static readonly IMultiRefReadOnlyByteArray AckRequest = new StaticReadOnlyByteArray(System.Text.Encoding.UTF8.GetBytes("AckTcp"));
        public static readonly IMultiRefReadOnlyByteArray AckOKResponse = new StaticReadOnlyByteArray(System.Text.Encoding.UTF8.GetBytes("AckTcp-OK"));
    }
}
