using System;
using Actuarius.Memory;

namespace Pontifex.Protocols.Reconnectable
{
    public static class ReconnectableInfo
    {
        public const string TransportName = "reconnectable";

        public const int ServerConnectionsLimit = 20000;
        public static readonly TimeSpan ReconnectionPeriod = TimeSpan.FromSeconds(2.0);

        public static readonly IMultiRefReadOnlyByteArray AckRequest = new StaticReadOnlyByteArray(System.Text.Encoding.UTF8.GetBytes("ReconnectableAck"));
        public static readonly IMultiRefReadOnlyByteArray AckOKResponse = new StaticReadOnlyByteArray(System.Text.Encoding.UTF8.GetBytes("ReconnectableAck-OK"));
    }
}