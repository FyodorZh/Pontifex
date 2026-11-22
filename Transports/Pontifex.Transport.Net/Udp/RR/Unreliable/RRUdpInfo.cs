using System;

namespace Pontifex.RR.Unreliable.Udp
{
    internal static class RRUdpInfo
    {
        public const string TransportName = "udp";
        public const int MessageMaxByteSize = 490;

        /// <summary>
        /// How long the client accepts a response after its request was sent.
        /// </summary>
        public static readonly TimeSpan DefaultClientRequestTtl = TimeSpan.FromSeconds(30);

        /// <summary>
        /// How long the server waits for user logic to respond after a request was received.
        /// </summary>
        public static readonly TimeSpan DefaultServerResponseTtl = TimeSpan.FromSeconds(10);
    }
}
