using System;

namespace Pontifex.RR.Direct
{
    /// <summary>
    /// Shared configuration of the in-process RR carriers.
    /// </summary>
    internal static class RRDirectInfo
    {
        public const string ReliableTransportName = "direct-rr-reliable";
        public const string UnreliableTransportName = "direct-rr-unreliable";

        /// <summary>
        /// The inclusive serialized payload limit of both RR Direct carriers. The request-id header is
        /// outside this limit.
        /// </summary>
        public const int MessageMaxByteSize = 1024 * 1024;

        /// <summary>
        /// The number of pre-start frames a server may hold before it releases the rest.
        /// </summary>
        public const int DispatchQueueCapacity = 100;

        /// <summary>
        /// The default client pending-window lifetime.
        /// </summary>
        public static readonly TimeSpan DefaultRequestTtl = TimeSpan.FromSeconds(3);

        /// <summary>
        /// The default server response-handle lifetime.
        /// </summary>
        public static readonly TimeSpan DefaultResponseTtl = TimeSpan.FromSeconds(3);
    }
}
