using System;

namespace Pontifex.Raw.Reliable.Reconnectable
{
    public static class ReconnectableInfo
    {
        public const string TransportName = "reconnectable";

        public const short NegotiationVersion = 1;

        public const int DefaultHandshakeMaxByteSize = 128;

        public const int ServerConnectionsLimit = 20000;

        public static readonly TimeSpan ReconnectionPeriod = TimeSpan.FromSeconds(2.0);
    }
}
