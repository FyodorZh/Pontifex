using Actuarius.Memory;
using Actuarius.PeriodicLogic;

namespace Transport.Protocols.Reconnectable
{
    public static class ReconnectableInfo
    {
        public const string TransportName = "reconnectable";

        public const int ServerConnectionsLimit = 20000;
        public static readonly DeltaTime ReconnectionPeriod = DeltaTime.FromSeconds(2.0);

        public static readonly IMultiRefReadOnlyByteArray AckRequest = new StaticReadOnlyByteArray(System.Text.Encoding.UTF8.GetBytes("ReconnectableAck"));
        public static readonly IMultiRefReadOnlyByteArray AckOKResponse = new StaticReadOnlyByteArray(System.Text.Encoding.UTF8.GetBytes("ReconnectableAck-OK"));
    }
}