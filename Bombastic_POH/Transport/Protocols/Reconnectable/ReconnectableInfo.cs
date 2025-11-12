namespace Transport.Protocols.Reconnectable
{
    public static class ReconnectableInfo
    {
        public const string TransportName = "reconnectable";

        public const int ServerConnectionsLimit = 20000;
        public static readonly Shared.DeltaTime ReconnectionPeriod = Shared.DeltaTime.FromSeconds(2.0);

        public static readonly byte[] AckRequest = System.Text.Encoding.UTF8.GetBytes("ReconnectableAck");
        public static readonly byte[] AckOKResponse = System.Text.Encoding.UTF8.GetBytes("ReconnectableAck-OK");
    }
}