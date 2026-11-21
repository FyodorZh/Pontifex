using System;
using System.Security.Cryptography;
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
    /// The class owns the client request mechanics: the fixed server route, client-local request ids,
    /// the per-request handler binding, the pending-request window, and the response dispatch. The
    /// lifecycle is inherited from <see cref="RRUnreliableTransport"/>; a carrier supplies the datagram
    /// transport through <see cref="RRUnreliableTransport.StartCarrier"/>,
    /// <see cref="RRUnreliableTransport.StopCarrier"/>, and <see cref="SendFrame"/> and reports
    /// inbound frames through <see cref="OnFrameReceived"/>.
    /// </remarks>
    public abstract class RRUnreliableClientTransport : RRUnreliableTransport, IRRUnreliableClientTransport
    {
        private readonly IEndPoint _serverEndPoint;
        private readonly TimeSpan _requestTtl;
        private readonly RRUnreliableRequestTable<IRRUnreliableRequestHandler> _pendingResponses;
        private long _nextRequestId;

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
            _requestTtl = requestTtl;
            _pendingResponses = new RRUnreliableRequestTable<IRRUnreliableRequestHandler>(
                timeProvider ?? UtcNowDateTimeProvider.Instance,
                requestTtl);
        }

        /// <inheritdoc/>
        public IEndPoint ServerEndPoint => _serverEndPoint;

        /// <inheritdoc/>
        public TimeSpan RequestTtl => _requestTtl;

        /// <summary>
        /// Starts the carrier and selects the random 16-bit base of this client's request ids.
        /// </summary>
        /// <returns><see langword="true"/> when the carrier started; otherwise
        /// <see langword="false"/>.</returns>
        protected override bool TryStart()
        {
            if (!base.TryStart())
            {
                return false;
            }

            _nextRequestId = CreateRandomRequestId() - 1;
            return true;
        }

        /// <summary>
        /// Returns the next 16-bit client-local request id. The value wraps unchecked.
        /// </summary>
        private ushort NextRequestId()
        {
            return unchecked((ushort)Interlocked.Increment(ref _nextRequestId));
        }

        private static ushort CreateRandomRequestId()
        {
            var bytes = new byte[sizeof(ushort)];

            using (RandomNumberGenerator generator = RandomNumberGenerator.Create())
            {
                generator.GetBytes(bytes);
            }

            return BitConverter.ToUInt16(bytes, 0);
        }

        /// <inheritdoc/>
        public SendResult Request(UnionDataList request, IRRUnreliableRequestHandler handler)
        {
            if (handler == null!)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            if (!(handler is RRRequestHandler bindable))
            {
                throw new ArgumentException(
                    "The handler must derive from RRRequestHandler.", nameof(handler));
            }

            if (bindable.IsBound)
            {
                if (bindable.RequestId is not ushort boundId ||
                    !_pendingResponses.TryReapExpired(boundId, handler))
                {
                    throw new InvalidOperationException(
                        "The handler is already bound to a pending request.");
                }

                bindable.Unbind();
            }

            if (!bindable.TryReserve())
            {
                throw new InvalidOperationException(
                    "The handler is already bound to a pending request.");
            }

            if (!TryValidatePayload(request, SendResult.NotConnected, out SendResult failure))
            {
                bindable.Unbind();
                return failure;
            }

            ushort requestId = NextRequestId();
            bindable.Bind(requestId, CancelRequest);
            _pendingResponses.Add(requestId, handler, UnbindExpired);
            RRUnreliableFrame.Encode(request, requestId);

            SendResult result = SendFrame(request);
            if (result != SendResult.Ok)
            {
                _pendingResponses.TryTakeExact(requestId, handler, out _, out _);
                bindable.Unbind();
                return result;
            }

            _pendingResponses.ReArm(requestId);
            return SendResult.Ok;
        }

        /// <summary>
        /// Cancels the request bound to <paramref name="handler"/>.
        /// </summary>
        private void CancelRequest(IRRRequestHandler handler)
        {
            if (handler == null!)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            if (!(handler is RRRequestHandler bindable) || !bindable.IsBound ||
                bindable.RequestId is not ushort requestId)
            {
                return;
            }

            if (_pendingResponses.TryTakeExact(requestId, handler, out _, out _))
            {
                bindable.Unbind();
            }
        }

        private static void UnbindExpired(IRRUnreliableRequestHandler handler)
        {
            ((RRRequestHandler)handler).Unbind();
        }

        /// <summary>
        /// Sends one encoded frame to the server.
        /// </summary>
        /// <param name="frame">The frame to send. The implementation owns it and must release it on every
        /// path.</param>
        protected abstract SendResult SendFrame(UnionDataList frame);

        /// <inheritdoc/>
        protected sealed override void ClearPendingRequests()
        {
            _pendingResponses.Clear(handler => ((RRRequestHandler)handler).Unbind());
        }

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

            if (!RRUnreliableFrame.TryDecode(frame, out ushort requestId))
            {
                Log.w("{0} received a malformed response frame from {1}", Name, source);
                frame.Release();
                return;
            }

            if (!_pendingResponses.TryTake(requestId, out IRRUnreliableRequestHandler handler,
                    out bool expired))
            {
                frame.Release();
                return;
            }

            if (!(handler is RRRequestHandler bindable))
            {
                frame.Release();
                return;
            }

            if (expired)
            {
                bindable.Unbind();
                frame.Release();
                return;
            }

            try
            {
                handler.Response(frame);
            }
            catch (Exception ex)
            {
                Log.wtf(ex);
            }
            finally
            {
                bindable.Unbind();
            }
        }
    }
}
