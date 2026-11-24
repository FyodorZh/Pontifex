using System;

namespace Pontifex.RR.Unreliable.Udp
{
    internal static class RRUdpInfo
    {
        public const string TransportName = "udp";

        /// <summary>
        /// The guaranteed UDP payload of the minimum IPv4 path: the 576-byte minimum MTU minus the
        /// 20-byte IPv4 header and the 8-byte UDP header.
        /// </summary>
        private const int MinIpv4UdpPayloadBytes = 548;

        /// <summary>
        /// The largest payload that leaves room for the largest response header and the growing
        /// element-count prefix inside one minimum-size datagram.
        /// </summary>
        public const int MessageMaxByteSize =
            MinIpv4UdpPayloadBytes - RRUnreliableFrame.OverheadBytes - 1;

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
