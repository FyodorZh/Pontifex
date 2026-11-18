using System;
using System.Threading;
using Actuarius.Memory;
using Pontifex.StopReasons;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Raw.Reliable.Ack
{
    /// <inheritdoc cref="IRawReliableAckServerEndpointImp"/>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Other notes</b>: The concrete transport must override BeginDisconnect, call the
    /// base implementation, and then call FinishDisconnect to complete teardown.</description></item>
    /// </list>
    /// </remarks>
    public abstract class RawReliableAckServerEndpoint : RawReliableServerEndpoint, IRawReliableAckServerEndpointImp
    {
        private readonly IRawReliableAckServerHandler _handler;

        private enum State
        {
            /// <summary>
            /// Before the low-level connection is completed and the ACK response is sent.
            /// </summary>
            Constructed = 0,

            /// <summary>
            /// Ack response sent and the session is logically connected.
            /// </summary>
            LogicallyConnected,

            /// <summary>
            /// The ack handshake failed or the low-level connection was destroyed.
            /// </summary>
            LogicallyDisconnected
        }

        private volatile State _stage;

        private UnionDataList? _pendingAckResponse;

        /// <summary>
        /// Stores the already-built acknowledgement response that is sent once the low-level connection is
        /// ready.
        /// </summary>
        /// <param name="pendingAckResponse">The acknowledgement response without its frame-type byte.
        /// Ownership transfers to the endpoint.</param>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Preconditions/Postconditions</b>: Must be called at most once, typically from
        /// the concrete endpoint's constructor.</description></item>
        /// <item><description><b>Other notes</b>: The buffered response is sent automatically from the first
        /// <see cref="OnConnected"/> transition.</description></item>
        /// </list>
        /// </remarks>
        protected void SetPendingAckResponse(UnionDataList pendingAckResponse)
        {
            if (pendingAckResponse == null)
            {
                throw new ArgumentNullException(nameof(pendingAckResponse));
            }

            if (Interlocked.CompareExchange(ref _pendingAckResponse, pendingAckResponse, null) != null)
            {
                throw new InvalidOperationException("The pending acknowledgement response is already set.");
            }
        }

        /// <summary>
        /// Releases the buffered acknowledgement response when it has not been sent yet.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Safe to call concurrently; only the first call releases
        /// the response.</description></item>
        /// </list>
        /// </remarks>
        protected void ReleasePendingAckResponse()
        {
            var ackResponse = Interlocked.Exchange(ref _pendingAckResponse, null);
            ackResponse?.Release();
        }

        /// <summary>
        /// Initializes a new reliable server endpoint that performs an acknowledgement handshake, with the given
        /// transport name, logging, and memory rental.
        /// </summary>
        /// <param name="transportName">The name of the transport that owns this endpoint.</param>
        /// <param name="remoteEndPoint">The remote endpoint address, or <see langword="null"/> when it is
        /// unknown.</param>
        /// <param name="handler">The session handler that receives the connection lifecycle
        /// notifications.</param>
        /// <param name="messageMaxByteSize">The inclusive maximum message size, in bytes, admitted by this
        /// endpoint; must be at least 1.</param>
        /// <param name="bufferCapacityBytes">The byte budget for messages accepted by this endpoint but not
        /// yet transmitted; must be at least 1.</param>
        /// <param name="memory">The memory rental system used to rent buffers.</param>
        /// <param name="log">The logging system used by this endpoint.</param>
        /// <param name="disconnectTimeout">The maximum time without an inbound message before the endpoint is
        /// stopped as timed out; must be positive.</param>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Other notes</b>: The handler is retained, not disposed, by the endpoint. The
        /// endpoint starts in the constructed stage.</description></item>
        /// </list>
        /// </remarks>
        public RawReliableAckServerEndpoint(string transportName, IEndPoint remoteEndPoint,
            IRawReliableAckServerHandler handler,
            int messageMaxByteSize, long bufferCapacityBytes, IMemoryRental memory, ILogger log,
            TimeSpan disconnectTimeout)
            : base(transportName, remoteEndPoint, handler, messageMaxByteSize, bufferCapacityBytes, memory, log, disconnectTimeout)
        {
            _handler = handler;

            // A terminal stop may happen before the endpoint is connected, so the pending response is
            // released here as well as from FailHard and BeginDisconnect. The release is idempotent.
            Stopped += OnEndpointStopped;
        }

        /// <summary>
        /// Gets whether the endpoint is logically connected, meaning the low-level connection is up and the ack
        /// handshake has completed successfully.
        /// </summary>
        /// <returns><see langword="true"/> when the endpoint is logically connected; otherwise
        /// <see langword="false"/>.</returns>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Safe to read concurrently.</description></item>
        /// <item><description><b>Other notes</b>: Returns false during and after the handler's disconnect
        /// notification.</description></item>
        /// </list>
        /// </remarks>
        protected override bool GetIsConnected()
        {
            return IsLowLevelConnected && _stage == State.LogicallyConnected;
        }

        /// <summary>
        /// Handles a fatal endpoint failure by notifying the handler when the endpoint was logically connected
        /// and moving the logical stage to disconnected before invoking the base implementation.
        /// </summary>
        /// <param name="reason">The error description that caused the failure.</param>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Invoked from the serial action queue's error
        /// handling.</description></item>
        /// <item><description><b>Other notes</b>: The logical stage is moved to disconnected before the
        /// disconnect notification, so IsConnected is false during that callback. The base implementation also
        /// delivers the endpoint's single terminal handler stop notification; the exact situations are documented
        /// on <see cref="RawReliableEndpoint"/>.</description></item>
        /// </list>
        /// </remarks>
        protected override void FailHard(StopReason reason)
        {
            ReleasePendingAckResponse();

            if (_stage == State.LogicallyConnected)
            {
                _stage = State.LogicallyDisconnected;
                try
                {
                    _handler.OnDisconnected(reason);
                }
                catch (Exception ex)
                {
                    Log.wtf(ex);
                }
            }
            _stage = State.LogicallyDisconnected;

            base.FailHard(reason);
        }

        /// <summary>
        /// Starts the inherited watchdog and sends the pending acknowledgement response once the endpoint is
        /// connected.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Called on the serial action queue, so the response is
        /// admitted for outbound delivery before the handler's
        /// <see cref="IRawReliableServerHandler.OnConnected"/> runs and inbound frames remain
        /// serialized.</description></item>
        /// <item><description><b>Other notes</b>: A missing pending response stops the endpoint instead of
        /// leaving the client to wait for its watchdog.</description></item>
        /// </list>
        /// </remarks>
        protected override void OnConnected()
        {
            base.OnConnected();

            var ackResponse = Interlocked.Exchange(ref _pendingAckResponse, null);
            if (ackResponse == null)
            {
                Stop(new TextFail(TransportName, "No pending ACK response"), false);
                return;
            }

            SendAckResponse(ackResponse);
        }

        /// <summary>
        /// Sends the already-built acknowledgement response and completes the logical connection.
        /// </summary>
        /// <param name="ackResponse">The acknowledgement response without its frame-type byte. The endpoint
        /// releases its own reference on every path.</param>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Called on the serial action queue from
        /// <see cref="OnConnected"/>.</description></item>
        /// <item><description><b>Preconditions/Postconditions</b>: A response arriving after the logical stage
        /// left <see cref="State.Constructed"/> is released and ignored. The handler observes
        /// <see cref="IRawReliableServerHandler.OnConnected"/> only after the response was admitted for
        /// outbound delivery.</description></item>
        /// <item><description><b>Other notes</b>: An exception moves the logical stage to disconnected and
        /// stops the endpoint with <see cref="ExceptionFail"/>; the handler receives no disconnect
        /// notification on that path.</description></item>
        /// </list>
        /// </remarks>
        private void SendAckResponse(UnionDataList ackResponse)
        {
            using var disposer = ackResponse.AsDisposable();

            if (_stage != State.Constructed)
            {
                return;
            }

            try
            {
                ackResponse.PutFirst((byte)MessageType.AckResponseMessage);

                if (ackResponse.GetDataSize() > RawReliableAckInfo.AckMaxByteSize)
                {
                    Stop(new TextFail(TransportName, "Ack response exceeds AckMaxByteSize"), false);
                    return;
                }

                if (ScheduleToSend(ackResponse.Acquire()) != SendResult.Ok)
                {
                    Stop(new TextFail(TransportName, "Failed to send ACK response"), false);
                    return;
                }

                _stage = State.LogicallyConnected;
                _handler.OnConnected(this);
            }
            catch (Exception ex)
            {
                _stage = State.LogicallyDisconnected;
                Log.wtf(ex);
                Stop(new ExceptionFail(TransportName, ex), false);
            }
        }

        /// <summary>
        /// Notifies the handler of the logical disconnection when the endpoint was logically connected and moves
        /// the logical stage to disconnected.
        /// </summary>
        /// <param name="reason">The reason the endpoint is being stopped.</param>
        /// <param name="tryBeGentle">A hint indicating whether a graceful disconnection should be
        /// attempted.</param>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Called on the serial action queue.</description></item>
        /// <item><description><b>Other notes</b>: This is a partial hook: it does not complete the stop
        /// transition. The concrete transport must override BeginDisconnect, call this base implementation, and
        /// then call FinishDisconnect to complete the stop.</description></item>
        /// </list>
        /// </remarks>
        protected override void BeginDisconnect(StopReason reason, bool tryBeGentle)
        {
            ReleasePendingAckResponse();

            if (_stage == State.LogicallyConnected)
            {
                _stage = State.LogicallyDisconnected;
                try
                {
                    _handler.OnDisconnected(reason);
                }
                catch (Exception ex)
                {
                    Log.wtf(ex);
                }
            }
            _stage = State.LogicallyDisconnected;
        }

        /// <summary>
        /// Releases the acknowledgement response that was never sent when the endpoint reaches its terminal
        /// stop.
        /// </summary>
        /// <param name="reason">The reason the endpoint stopped; not used.</param>
        private void OnEndpointStopped(StopReason reason)
        {
            ReleasePendingAckResponse();
        }
    }
}
