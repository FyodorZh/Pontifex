using System;
using Actuarius.Memory;
using Pontifex.Endpoints;
using Pontifex.StopReasons;
using Scriba;

namespace Pontifex.Raw.Reliable.Ack.Direct
{
    /// <summary>
    /// Reliable acknowledgement client transport carried by an in-process <see cref="Channel"/>.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Thread safety</b>: Lifecycle calls are serialized by the base transport lock;
    /// the endpoint performs its own connect and teardown work on its serial action queue.</description></item>
    /// <item><description><b>Other notes</b>: The in-process handshake completes synchronously when a server is
    /// available. Connection failures are not allowed to turn the start into a failure, like the asynchronous
    /// TCP carrier: <c>Start</c> succeeds and a missing, rejecting, or immediately disconnecting server is
    /// reported through the handler's <c>OnStopped</c> without <c>OnConnected</c>.</description></item>
    /// </list>
    /// </remarks>
    public sealed class RawReliableAckDirectClientTransport : RawReliableAckClientTransport
    {
        private readonly StringEndPoint _serverEndPoint;
        private readonly GuidEndPoint _clientEndPoint;
        private readonly int _messageMaxByteSize;
        private readonly long _bufferCapacityBytes;

        private StopReason? _pendingFailure;

        public override int MessageMaxByteSize => _messageMaxByteSize;

        /// <summary>
        /// Initializes a reliable acknowledgement direct client transport.
        /// </summary>
        /// <param name="serverName">The name of the server to connect to.</param>
        /// <param name="logger">The logging system used by the transport.</param>
        /// <param name="memory">The memory rental system used by the transport.</param>
        /// <param name="messageMaxSize">The inclusive maximum message size, or <see langword="null"/> for the
        /// direct default.</param>
        /// <param name="bufferCapacityBytes">The byte budget for messages accepted but not yet transmitted, or
        /// <see langword="null"/> for the direct default.</param>
        public RawReliableAckDirectClientTransport(
            string serverName,
            ILogger logger,
            IMemoryRental memory,
            int? messageMaxSize = null,
            long? bufferCapacityBytes = null)
            : base(DirectInfo.TransportName, logger, memory)
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

            _serverEndPoint = new StringEndPoint(serverName);
            _clientEndPoint = new GuidEndPoint(Guid.NewGuid());
            _messageMaxByteSize = messageMaxSize ?? DirectInfo.DefaultMessageMaxSize;
            _bufferCapacityBytes = bufferCapacityBytes ?? DirectInfo.DefaultBufferCapacity;
        }

        protected override bool BeginConnect()
        {
            var handler = Handler;
            if (handler == null)
            {
                Log.e("Transport handler is not set");
                return false;
            }

            try
            {
                var endpoint = new RawReliableAckDirectClientEndpoint(
                    Name,
                    _serverEndPoint,
                    _clientEndPoint,
                    handler,
                    _messageMaxByteSize,
                    _bufferCapacityBytes,
                    Memory,
                    Log);

                AttachEndpoint(endpoint);

                if (!endpoint.Start())
                {
                    endpoint.FinishTransportStart();
                    Log.e("Failed to start the client endpoint");
                    return false;
                }

                _pendingFailure = endpoint.TakePendingStopReason();
                return true;
            }
            catch (Exception ex)
            {
                Log.wtf(ex);
                return false;
            }
        }

        protected override void OnReadyToConnect()
        {
            var failure = _pendingFailure;
            _pendingFailure = null;

            if (AttachedEndpoint is RawReliableAckDirectClientEndpoint endpoint)
            {
                failure ??= endpoint.TakePendingStopReason();
                endpoint.FinishTransportStart();

                // A failure may have been reported between the first observation and the deferral end.
                failure ??= endpoint.TakePendingStopReason();
            }

            if (failure != null)
            {
                // A synchronous connection failure must not turn the start into a failure. Report it after
                // the start transition has been accepted instead, like the asynchronous TCP carrier.
                Stop(failure);
            }
        }

        protected override void DestroyTransport(StopReason reason)
        {
            var endpoint = AttachedEndpoint;
            if (endpoint == null)
            {
                return;
            }

            try
            {
                endpoint.Stop(reason, false);
            }
            catch (Exception ex)
            {
                Log.wtf(ex);
            }
        }

        public override string ToString()
        {
            try
            {
                return $"direct-client[{_serverEndPoint}]";
            }
            catch (Exception)
            {
                return "direct-client[unknown]";
            }
        }
    }
}
