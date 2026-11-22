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
    /// The class owns the server request mechanics: server-global request ids, response handles, and the
    /// request dispatch. The lifecycle is inherited from
    /// <see cref="RRUnreliableTransport{THandler}"/>; a carrier supplies the datagram transport through
    /// <see cref="RRUnreliableTransport.StartCarrier"/>,
    /// <see cref="RRUnreliableTransport.StopCarrier"/>, and <see cref="SendFrame"/> and reports
    /// inbound frames through <see cref="OnFrameReceived"/>.
    /// </remarks>
    public abstract class RRUnreliableServerTransport
        : RRUnreliableTransport<IRRUnreliableServerHandler>, IRRUnreliableServerTransport
    {
        private readonly TimeSpan _responseTtl;
        private readonly IDateTimeProvider _timeProvider;

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
            if (responseTtl <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(responseTtl), "The TTL must be positive.");
            }

            _responseTtl = responseTtl;
            _timeProvider = timeProvider ?? UtcNowDateTimeProvider.Instance;
        }

        /// <inheritdoc/>
        public TimeSpan ResponseTtl => _responseTtl;

        /// <summary>
        /// Sends one encoded frame to <paramref name="destination"/>.
        /// </summary>
        /// <param name="destination">The route that received the request.</param>
        /// <param name="frame">The frame to send. The implementation owns it and must release it on every
        /// path.</param>
        protected abstract SendResult SendFrame(IEndPoint destination, UnionDataList frame);

        /// <summary>
        /// Handles one inbound datagram received by the concrete carrier.
        /// </summary>
        /// <param name="source">The route that sent the datagram.</param>
        /// <param name="frame">The received frame. The method owns it.</param>
        /// <remarks>
        /// The dispatch gate is read before <see cref="IRRUnreliableServerHandler.OnRequested"/> is
        /// invoked, without synchronizing with the stop transition. A frame that passes the check
        /// just before the transport stops may still reach the handler, possibly after
        /// <c>onStopped</c> has run. The response handle is already closed, so the handler can observe
        /// the request but cannot answer it. A callback dispatcher would close this window.
        /// </remarks>
        protected void OnFrameReceived(IEndPoint source, UnionDataList frame)
        {
            if (frame == null!)
            {
                return;
            }

            if (!RRUnreliableFrame.TryDecode(frame, out ushort clientRequestId))
            {
                Log.w("{0} received a malformed request frame from {1}", Name, source);
                frame.Release();
                return;
            }

            if (frame.GetDataSize() > MessageMaxByteSize)
            {
                Log.w("{0} dropped an oversized request from {1}", Name, source);
                frame.Release();
                return;
            }

            IRRUnreliableServerHandler? handler = DispatchHandler;
            if (handler == null)
            {
                frame.Release();
                return;
            }

            long requestId = NextServerRequestId();
            var response = new RRUnreliableResponse(
                this,
                requestId,
                _timeProvider.Now + _responseTtl,
                source,
                clientRequestId);

            bool accepted;
            try
            {
                accepted = handler.OnRequested(source, frame, response);
            }
            catch (Exception ex)
            {
                Log.wtf(ex);
                accepted = false;
            }

            if (!accepted)
            {
                response.Close();
            }
        }

        /// <summary>
        /// Sends the single response held by <paramref name="response"/>.
        /// </summary>
        /// <param name="response">The response handle that owns the route.</param>
        /// <param name="payload">The response payload. The method owns it.</param>
        /// <returns>The result of the send attempt.</returns>
        internal SendResult SendResponse(RRUnreliableResponse response, UnionDataList payload)
        {
            if (response.IsClosed || !IsStarted || _timeProvider.Now > response.ExpiresAt)
            {
                if (!response.IsClosed)
                {
                    response.Close();
                }

                payload?.Release();
                return SendResult.Error;
            }

            if (payload == null!)
            {
                return SendResult.InvalidMessage;
            }

            int size;
            try
            {
                size = payload.GetDataSize();
            }
            catch (Exception)
            {
                return SendResult.InvalidMessage;
            }

            if (size > MessageMaxByteSize)
            {
                payload.Release();
                return SendResult.MessageTooBig;
            }

            if (!response.TryClose())
            {
                payload.Release();
                return SendResult.Error;
            }

            RRUnreliableFrame.Encode(payload, response.ClientRequestId);

            try
            {
                return SendFrame(response.Client, payload);
            }
            catch (Exception ex)
            {
                Log.wtf(ex);
                return SendResult.Error;
            }
        }
    }
}
