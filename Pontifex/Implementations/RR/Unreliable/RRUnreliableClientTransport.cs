using System;
using System.Threading;
using Actuarius.Memory;
using Operarius;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.RR.Unreliable
{
    /// <summary>
    /// Base class for RR unreliable client transports.
    /// </summary>
    /// <remarks>
    /// The class owns the request/response mechanics: client-local request ids, the pending-request
    /// window, and the frame header. A carrier supplies the datagram transport through
    /// <see cref="StartCarrier"/>, <see cref="StopCarrier"/>, and <see cref="SendFrame"/> and reports
    /// inbound frames through <see cref="OnFrameReceived"/>.
    /// </remarks>
    public abstract class RRUnreliableClientTransport : AnyTransport, IRRUnreliableClientTransport
    {
        private readonly IEndPoint _serverEndPoint;
        private readonly RRUnreliableRequestTable<byte> _pendingResponses;

        private volatile IRRUnreliableClientHandler? _handler;
        private volatile bool _handlerStarted;
        private bool _initialized;
        private long _lastRequestId;

        /// <summary>
        /// Initializes a new RR unreliable client transport.
        /// </summary>
        /// <param name="typeName">The name of the concrete transport.</param>
        /// <param name="serverEndPoint">The fixed route of the server. Must not be
        /// <see langword="null"/>.</param>
        /// <param name="requestTtl">How long a response is accepted after its request was sent. Must be
        /// positive.</param>
        /// <param name="logger">The logging system used by the transport.</param>
        /// <param name="memory">The memory rental system used to rent buffers.</param>
        /// <param name="timeProvider">The clock used by the request TTL, or <see langword="null"/> for the
        /// system UTC clock.</param>
        protected RRUnreliableClientTransport(string typeName, IEndPoint serverEndPoint, TimeSpan requestTtl,
            ILogger logger, IMemoryRental memory, IDateTimeProvider? timeProvider = null)
            : base(typeName, logger, memory)
        {
            _serverEndPoint = serverEndPoint ?? throw new ArgumentNullException(nameof(serverEndPoint));
            _pendingResponses = new RRUnreliableRequestTable<byte>(
                timeProvider ?? UtcNowDateTimeProvider.Instance,
                requestTtl);
        }

        /// <inheritdoc/>
        public IEndPoint ServerEndPoint => _serverEndPoint;

        /// <inheritdoc/>
        public abstract int MessageMaxByteSize { get; }

        /// <summary>
        /// Gets the inclusive carrier-frame size that fits <see cref="MessageMaxByteSize"/> plus the
        /// request-id header.
        /// </summary>
        protected int FrameMaxByteSize => MessageMaxByteSize + RRUnreliableFrame.OverheadBytes;

        /// <inheritdoc/>
        public bool Init(IRRUnreliableClientHandler handler)
        {
            if (handler == null!)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            lock (_locker)
            {
                if (_initialized || HasStartBeenAttempted || !IsValid)
                {
                    return false;
                }

                _initialized = true;
                _handler = handler;
                return true;
            }
        }

        /// <inheritdoc/>
        public SendResult Request(UnionDataList request, out long requestId)
        {
            requestId = -1;

            if (request == null!)
            {
                return SendResult.InvalidMessage;
            }

            if (!IsStarted)
            {
                request.Release();
                return SendResult.NotConnected;
            }

            if (request.GetDataSize() > MessageMaxByteSize)
            {
                request.Release();
                return SendResult.MessageTooBig;
            }

            long id = Interlocked.Increment(ref _lastRequestId);
            _pendingResponses.Add(id, 0);
            RRUnreliableFrame.Encode(request, id);

            SendResult result = SendFrame(request);
            if (result != SendResult.Ok)
            {
                _pendingResponses.TryTake(id, out _);
                return result;
            }

            requestId = id;
            return SendResult.Ok;
        }

        /// <summary>
        /// Starts the concrete carrier.
        /// </summary>
        /// <returns><see langword="true"/> when the carrier started; otherwise
        /// <see langword="false"/>.</returns>
        protected abstract bool StartCarrier();

        /// <summary>
        /// Stops and releases the concrete carrier. Must tolerate a carrier that never fully started.
        /// </summary>
        protected abstract void StopCarrier(StopReason reason);

        /// <summary>
        /// Sends one encoded frame to the server.
        /// </summary>
        /// <param name="frame">The frame to send. The implementation owns it and must release it on every
        /// path.</param>
        protected abstract SendResult SendFrame(UnionDataList frame);

        /// <summary>
        /// Handles one inbound datagram received by the concrete carrier.
        /// </summary>
        /// <param name="source">The source route of the datagram.</param>
        /// <param name="frame">The received frame. The method owns it.</param>
        protected void OnFrameReceived(IEndPoint source, UnionDataList frame)
        {
            if (frame == null!)
            {
                return;
            }

            if (!RRUnreliableFrame.TryDecode(frame, out long requestId))
            {
                Log.w("{0} received a malformed response frame from {1}", Name, source);
                frame.Release();
                return;
            }

            if (!_pendingResponses.TryTake(requestId, out _))
            {
                frame.Release();
                return;
            }

            IRRUnreliableClientHandler? handler = _handler;
            if (handler == null)
            {
                frame.Release();
                return;
            }

            if (!_handlerStarted)
            {
                // The carrier may receive before Started() returns; such a frame is dropped.
                frame.Release();
                return;
            }

            try
            {
                handler.OnResponded(requestId, frame);
            }
            catch (Exception ex)
            {
                FailException($"{handler.GetType()}.OnResponded()", ex);
            }
        }

        /// <inheritdoc/>
        protected override bool TryStart()
        {
            IRRUnreliableClientHandler? handler = _handler;
            if (handler == null)
            {
                return false;
            }

            bool carrierStarted;
            try
            {
                carrierStarted = StartCarrier();
            }
            catch (Exception ex)
            {
                FailException("StartCarrier", ex);
                return false;
            }

            if (!carrierStarted)
            {
                return false;
            }

            try
            {
                handler.Started();
            }
            catch (Exception ex)
            {
                FailException($"{handler.GetType()}.Started()", ex);
                return false;
            }

            _handlerStarted = true;
            return true;
        }

        /// <inheritdoc/>
        protected override void OnStarted()
        {
            // DO NOTHING
        }

        /// <inheritdoc/>
        protected override void OnStopped(StopReason reason)
        {
            IRRUnreliableClientHandler? handler = _handler;
            _handler = null;
            _handlerStarted = false;

            if (handler != null)
            {
                try
                {
                    handler.Stopped();
                }
                catch (Exception ex)
                {
                    Log.wtf(ex);
                }
            }

            _pendingResponses.Clear();

            try
            {
                StopCarrier(reason);
            }
            catch (Exception ex)
            {
                Log.wtf(ex);
            }
        }
    }
}
