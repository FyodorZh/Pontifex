using System;
using Actuarius.Memory;
using Pontifex.StopReasons;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Raw.Reliable.Ack
{
    /// <summary>
    /// Abstract base class for a reliable server endpoint that completes the acknowledgement handshake before
    /// the logical connection is established. Extends <see cref="RawReliableServerEndpoint"/> with a logical
    /// stage that answers the client's ack request with an ack response.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Thread safety</b>: All public and protected instance members are safe to call
    /// concurrently from any thread. The ack stage is volatile, and the handshake completion and handler
    /// notifications are marshalled onto the serial action queue.</description></item>
    /// <item><description><b>Other notes</b>: The concrete transport must override BeginDisconnect, call the
    /// base implementation, and then call FinishDisconnect to complete teardown. An ack request received
    /// outside the expected stage is treated as a fatal protocol violation. The client's acknowledgement data
    /// is released by this endpoint; validating it is the responsibility of the caller that created the
    /// session handler.</description></item>
    /// </list>
    /// </remarks>
    public abstract class RawReliableAckServerEndpoint : RawReliableServerEndpoint
    {
        private readonly IRawReliableAckServerHandler _handler;

        private enum State
        {
            /// <summary>
            /// Before the client's ack request is received.
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

        /// <summary>
        /// Initializes a new reliable server endpoint that performs an acknowledgement handshake, with the given
        /// transport name, logging, and memory rental.
        /// </summary>
        /// <param name="transportName">The name of the transport that owns this endpoint.</param>
        /// <param name="remoteEndPoint">The remote endpoint address, or <see langword="null"/> when it is
        /// unknown.</param>
        /// <param name="handler">The session handler that provides the ack response data and receives the
        /// connection lifecycle notifications.</param>
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
        /// delivers the stop notification when the endpoint had started.</description></item>
        /// </list>
        /// </remarks>
        protected override void FailHard(StopReason reason)
        {
            if (_stage == State.LogicallyConnected)
            {
                _stage = State.LogicallyDisconnected;
                _handler.OnDisconnected(reason);
            }
            _stage = State.LogicallyDisconnected;

            base.FailHard(reason);
        }
        
        private void FinishAckConnect(UnionDataList message)
        {
            _actionQueue.Schedule(() =>
            {
                try
                {
                    switch (_stage)
                    {
                        case State.Constructed:
                            message.Release();
                            if (SendAckResponse())
                            {
                                _stage = State.LogicallyConnected;
                                _handler.OnConnected(this);
                            }
                            break;

                        case State.LogicallyConnected:
                            message.Release();
                            throw new InvalidOperationException($"Wrong RawReliableAckServerEndpoint stage: '{_stage}'");

                        case State.LogicallyDisconnected:
                            message.Release();
                            break;

                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
                catch (Exception ex)
                {
                    _stage = State.LogicallyDisconnected;
                    Log.wtf(ex);
                    Stop(new ExceptionFail(TransportName, ex), false);
                }
            });
        }

        private bool SendAckResponse()
        {
            UnionDataList ackResponse = Memory.CollectablePool.Acquire<UnionDataList>();
            using var disposer = ackResponse.AsDisposable();
            try
            {
                _handler.FillAckResponse(ackResponse);
                ackResponse.PutFirst((byte)MessageType.AckResponseMessage);

                if (ScheduleToSend(ackResponse.Acquire()) != SendResult.Ok)
                {
                    Stop(new TextFail(TransportName, "Failed to send ACK response"), false);
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Log.wtf(ex);
                _stage = State.LogicallyDisconnected;
                Stop(new TextFail(TransportName, "FillAckResponse throw an exception"), false);
                return false;
            }
        }

        /// <summary>
        /// Handles an acknowledgement request by completing the ack handshake, and delegates all other frame
        /// types to the base implementation.
        /// </summary>
        /// <param name="messageType">The frame type popped from the message.</param>
        /// <param name="message">The frame body. An acknowledgement request is released by this method.</param>
        /// <returns><see langword="true"/> when the frame was handled; otherwise
        /// <see langword="false"/>.</returns>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Invoked on the caller's thread; the acknowledgement
        /// completion runs on the serial action queue, so handler callbacks remain
        /// serialized.</description></item>
        /// <item><description><b>Side effects</b>: Refreshes the idle-session watchdog timestamp for an
        /// acknowledgement request.</description></item>
        /// <item><description><b>Other notes</b>: An acknowledgement request received outside the expected
        /// stage is treated as a fatal protocol violation. Overrides must delegate non-acknowledgement frames to
        /// the base implementation.</description></item>
        /// </list>
        /// </remarks>
        protected override bool ProcessInboundMessage(MessageType messageType, UnionDataList message)
        {
            if (messageType == MessageType.AckRequestMessage)
            {
                RefreshLastInboundMessage();
                FinishAckConnect(message);
                return true;
            }

            return base.ProcessInboundMessage(messageType, message);
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
            if (_stage == State.LogicallyConnected)
            {
                _stage = State.LogicallyDisconnected;
                _handler.OnDisconnected(reason);
            }
            _stage = State.LogicallyDisconnected;
        }
    }
}
