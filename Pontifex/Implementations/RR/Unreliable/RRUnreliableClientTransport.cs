using System;
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
    /// the pending-request window, and the response dispatch. The lifecycle is inherited from
    /// <see cref="RRUnreliableTransport{THandler}"/>; a carrier supplies the datagram transport through
    /// <see cref="RRUnreliableTransport{THandler}.StartCarrier"/>,
    /// <see cref="RRUnreliableTransport{THandler}.StopCarrier"/>, and <see cref="SendFrame"/> and reports
    /// inbound frames through <see cref="OnFrameReceived"/>.
    /// </remarks>
    public abstract class RRUnreliableClientTransport
        : RRUnreliableTransport<IRRUnreliableClientHandler>, IRRUnreliableClientTransport
    {
        private readonly IEndPoint _serverEndPoint;
        private readonly RRUnreliableRequestTable<byte> _pendingResponses;

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
        public SendResult Request(UnionDataList request, out long requestId)
        {
            requestId = -1;

            if (!TryValidatePayload(request, SendResult.NotConnected, out SendResult failure))
            {
                return failure;
            }

            long id = NextRequestId();
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
        /// Sends one encoded frame to the server.
        /// </summary>
        /// <param name="frame">The frame to send. The implementation owns it and must release it on every
        /// path.</param>
        protected abstract SendResult SendFrame(UnionDataList frame);

        /// <inheritdoc/>
        protected sealed override void ClearPendingRequests()
        {
            _pendingResponses.Clear();
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

            IRRUnreliableClientHandler? handler = DispatchHandler;
            if (handler == null)
            {
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
    }
}
