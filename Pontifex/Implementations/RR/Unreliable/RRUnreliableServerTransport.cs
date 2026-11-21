using System;
using Actuarius.Memory;
using Operarius;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.RR.Unreliable
{
    /// <summary>
    /// Base class for RR unreliable server transports.
    /// </summary>
    /// <remarks>
    /// The class owns the server request mechanics: server-global request ids, the pending-response
    /// routes, and the request dispatch. The lifecycle is inherited from
    /// <see cref="RRUnreliableTransport{THandler}"/>; a carrier supplies the datagram transport through
    /// <see cref="RRUnreliableTransport{THandler}.StartCarrier"/>,
    /// <see cref="RRUnreliableTransport{THandler}.StopCarrier"/>, and <see cref="SendFrame"/> and reports
    /// inbound frames through <see cref="OnFrameReceived"/>.
    /// </remarks>
    public abstract class RRUnreliableServerTransport
        : RRUnreliableTransport<IRRUnreliableServerHandler>, IRRUnreliableServerTransport
    {
        private readonly struct PendingResponse
        {
            public PendingResponse(IEndPoint client, long clientRequestId)
            {
                Client = client;
                ClientRequestId = clientRequestId;
            }

            /// <summary>
            /// The route that received the request.
            /// </summary>
            public IEndPoint Client { get; }

            /// <summary>
            /// The client-local id that the response frame must carry back.
            /// </summary>
            public long ClientRequestId { get; }
        }

        private readonly RRUnreliableRequestTable<PendingResponse> _pendingResponses;

        /// <summary>
        /// Initializes a new RR unreliable server transport.
        /// </summary>
        /// <param name="typeName">The name of the concrete transport.</param>
        /// <param name="responseTtl">How long user logic may take to respond after a request was received.
        /// Must be positive.</param>
        /// <param name="logger">The logging system used by the transport.</param>
        /// <param name="memory">The memory rental system used to rent buffers.</param>
        /// <param name="timeProvider">The clock used by the response TTL, or <see langword="null"/> for the
        /// system UTC clock.</param>
        protected RRUnreliableServerTransport(string typeName, TimeSpan responseTtl, ILogger logger,
            IMemoryRental memory, IDateTimeProvider? timeProvider = null)
            : base(typeName, logger, memory)
        {
            _pendingResponses = new RRUnreliableRequestTable<PendingResponse>(
                timeProvider ?? UtcNowDateTimeProvider.Instance,
                responseTtl);
        }

        /// <inheritdoc/>
        public SendResult Response(long requestId, UnionDataList response)
        {
            if (response == null!)
            {
                return SendResult.InvalidMessage;
            }

            if (!IsStarted)
            {
                response.Release();
                return SendResult.Error;
            }

            if (response.GetDataSize() > MessageMaxByteSize)
            {
                response.Release();
                return SendResult.MessageTooBig;
            }

            if (!_pendingResponses.TryTake(requestId, out PendingResponse route))
            {
                response.Release();
                return SendResult.InvalidAddress;
            }

            RRUnreliableFrame.Encode(response, route.ClientRequestId);
            return SendFrame(route.Client, response);
        }

        /// <summary>
        /// Sends one encoded frame to <paramref name="destination"/>.
        /// </summary>
        /// <param name="destination">The route that received the request.</param>
        /// <param name="frame">The frame to send. The implementation owns it and must release it on every
        /// path.</param>
        protected abstract SendResult SendFrame(IEndPoint destination, UnionDataList frame);

        /// <inheritdoc/>
        protected sealed override void ClearPendingRequests()
        {
            _pendingResponses.Clear();
        }

        /// <summary>
        /// Handles one inbound datagram received by the concrete carrier.
        /// </summary>
        /// <param name="source">The route that sent the datagram.</param>
        /// <param name="frame">The received frame. The method owns it.</param>
        protected void OnFrameReceived(IEndPoint source, UnionDataList frame)
        {
            if (frame == null!)
            {
                return;
            }

            if (!RRUnreliableFrame.TryDecode(frame, out long clientRequestId))
            {
                Log.w("{0} received a malformed request frame from {1}", Name, source);
                frame.Release();
                return;
            }

            IRRUnreliableServerHandler? handler = DispatchHandler;
            if (handler == null)
            {
                frame.Release();
                return;
            }

            long requestId = NextRequestId();
            _pendingResponses.Add(requestId, new PendingResponse(source, clientRequestId));

            if (!handler.OnRequested(requestId, source, frame))
            {
                _pendingResponses.TryTake(requestId, out _);
            }
        }
    }
}
