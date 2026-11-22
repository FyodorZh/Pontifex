using System;
using Actuarius.Memory;
using Pontifex.Endpoints;
using Scriba;

namespace Pontifex.Raw.Reliable.Direct
{
    /// <summary>
    /// Reliable client transport carried by an in-process <see cref="Channel"/>.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Thread safety</b>: Lifecycle calls are serialized by the base transport lock;
    /// the endpoint performs its own connect and teardown work on its serial action queue.</description></item>
    /// <item><description><b>Transport handshake</b>: the client sends the handshake request as the first
    /// channel message and becomes logically connected only after a matching response. The binding scheme and
    /// the handshake name are both <c>direct</c>.</description></item>
    /// <item><description><b>Other notes</b>: The in-process handshake completes synchronously when a server is
    /// available. A missing, rejecting, or immediately disconnecting server is reported through the handler's
    /// <c>OnStopped</c> without <c>OnConnected</c>; it is not a start failure.</description></item>
    /// </list>
    /// </remarks>
    public sealed class RawReliableDirectClientTransport
        : RawReliableEndpointClientTransport<IRawReliableClientHandler>, IRawReliableClient
    {
        private readonly StringEndPoint _serverEndPoint;
        private readonly GuidEndPoint _clientEndPoint;
        private readonly int _messageMaxByteSize;
        private readonly long _bufferCapacityBytes;
        private readonly string _handshakeName;
        private readonly int _handshakeMaxByteSize;

        public override int MessageMaxByteSize => _messageMaxByteSize;

        /// <summary>
        /// Initializes a reliable Direct client transport.
        /// </summary>
        /// <param name="serverName">The name of the server to connect to.</param>
        /// <param name="logger">The logging system used by the transport.</param>
        /// <param name="memory">The memory rental system used by the transport.</param>
        /// <param name="messageMaxSize">The inclusive maximum message size, or <see langword="null"/> for the
        /// direct default.</param>
        /// <param name="bufferCapacityBytes">The byte budget for messages accepted but not yet transmitted, or
        /// <see langword="null"/> for the direct default.</param>
        /// <param name="handshakeName">The transport handshake name sent to the server.</param>
        /// <param name="handshakeMaxByteSize">The inclusive maximum size of a handshake frame. The in-process
        /// handshake is synchronous, so there is no handshake timeout parameter.</param>
        public RawReliableDirectClientTransport(
            string serverName,
            ILogger logger,
            IMemoryRental memory,
            int? messageMaxSize = null,
            long? bufferCapacityBytes = null,
            string handshakeName = RawReliableDirectInfo.HandshakeName,
            int handshakeMaxByteSize = RawReliableDirectInfo.DefaultHandshakeMaxByteSize)
            : base(RawReliableDirectInfo.TransportName, logger, memory)
        {
            if (serverName == null)
            {
                throw new ArgumentNullException(nameof(serverName));
            }

            if (messageMaxSize.HasValue && messageMaxSize.Value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(messageMaxSize));
            }

            if (bufferCapacityBytes.HasValue && bufferCapacityBytes.Value < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(bufferCapacityBytes));
            }

            if (handshakeName == null)
            {
                throw new ArgumentNullException(nameof(handshakeName));
            }

            if (handshakeMaxByteSize < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(handshakeMaxByteSize));
            }

            _serverEndPoint = new StringEndPoint(serverName);
            _clientEndPoint = new GuidEndPoint(Guid.NewGuid());
            _messageMaxByteSize = messageMaxSize ?? DirectInfo.DefaultMessageMaxSize;
            _bufferCapacityBytes = bufferCapacityBytes ?? DirectInfo.DefaultBufferCapacity;
            _handshakeName = handshakeName;
            _handshakeMaxByteSize = handshakeMaxByteSize;

            if (_messageMaxByteSize < _handshakeMaxByteSize)
            {
                throw new ArgumentOutOfRangeException(nameof(messageMaxSize),
                    "The message maximum must be at least the handshake maximum.");
            }
        }

        /// <summary>
        /// Gets the endpoint attached to the current connection attempt, for diagnostics and tests.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Other notes</b>: The endpoint is present from the start transition until the
        /// transport detaches it during teardown.</description></item>
        /// </list>
        /// </remarks>
        internal RawReliableDirectClientEndpoint? CurrentEndpoint =>
            AttachedEndpoint as RawReliableDirectClientEndpoint;

        protected override IRawReliableClientEndpointImp CreateEndpoint(IRawReliableClientHandler handler)
        {
            return new RawReliableDirectClientEndpoint(
                Name,
                _serverEndPoint,
                _clientEndPoint,
                handler,
                _messageMaxByteSize,
                _bufferCapacityBytes,
                Memory,
                Log,
                _handshakeName,
                _handshakeMaxByteSize);
        }

        public override string ToString()
        {
            try
            {
                return $"{RawReliableDirectInfo.TransportName}-client[{_serverEndPoint}]";
            }
            catch (Exception)
            {
                return $"{RawReliableDirectInfo.TransportName}-client[unknown]";
            }
        }
    }
}
