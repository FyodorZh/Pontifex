using System;
using Actuarius.Memory;
using Pontifex.StopReasons;
using Pontifex.Utils;

namespace Pontifex.Raw.Reliable.Ack
{
    /// <summary>
    /// Abstract base class for a reliable client endpoint that performs an acknowledgement handshake before the
    /// logical connection is established. Extends <see cref="RawReliableClientEndpoint"/> with a logical stage
    /// that sends an ack request and waits for the server's ack response.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Thread safety</b>: All public and protected instance members are safe to call
    /// concurrently from any thread. The ack stage is volatile, and the handshake completion and handler
    /// notifications are marshalled onto the serial action queue.</description></item>
    /// <item><description><b>Other notes</b>: The concrete transport must override BeginDisconnect, call the
    /// base implementation, and then call FinishDisconnect to complete teardown. An ack response received
    /// outside the expected stage is treated as a fatal protocol violation.</description></item>
    /// </list>
    /// </remarks>
    public abstract class RawReliableAckClientEndpoint : RawReliableClientEndpoint
    {
        private readonly IRawReliableAckClientHandler _handler;

        private enum State
        {
            /// <summary>
            /// Before the beginning of a low-level connection
            /// </summary>
            Constructed = 0,
            
            /// <summary>
            /// Ack request sent but ack response is not received
            /// </summary>
            AckRequestSent,

            /// <summary>
            /// Ack response received and approved
            /// </summary>
            LogicallyConnected,

            /// <summary>
            /// Low-level connection destroyed
            /// </summary>
            LogicallyDisconnected
        }

        private volatile State _stage;

        /// <summary>
        /// Initializes a new reliable client endpoint that performs an acknowledgement handshake, owned by the
        /// given transport.
        /// </summary>
        /// <param name="owner">The owning transport that provides the transport name, logging, and memory
        /// rental.</param>
        /// <param name="remoteEndPoint">The remote endpoint address, or <see langword="null"/> when it is
        /// unknown.</param>
        /// <param name="handler">The handler that provides the ack request data and receives the connection
        /// lifecycle notifications.</param>
        /// <param name="messageMaxByteSize">The inclusive maximum message size, in bytes, admitted by this
        /// endpoint; must be at least 1.</param>
        /// <param name="bufferCapacityBytes">The byte budget for messages accepted by this endpoint but not
        /// yet transmitted; must be at least 1.</param>
        /// <param name="pingOptions">The keep-alive configuration, or <see langword="null"/> to disable
        /// keep-alive.</param>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Other notes</b>: The owning transport and the handler are retained, not
        /// disposed, by the endpoint. The endpoint starts in the constructed stage.</description></item>
        /// </list>
        /// </remarks>
        protected RawReliableAckClientEndpoint(IRawReliableTransport owner,
            IEndPoint remoteEndPoint, IRawReliableAckClientHandler handler,
            int messageMaxByteSize, long bufferCapacityBytes,
            PingOptions? pingOptions)
            : base(owner, remoteEndPoint, handler, messageMaxByteSize, bufferCapacityBytes, pingOptions)
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

        /// <summary>
        /// Initializes the inherited keep-alive behavior and begins the acknowledgement handshake.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Called on the serial action queue.</description></item>
        /// <item><description><b>Other notes</b>: The ack request is sent from a deferred action; if the
        /// endpoint is no longer connected when that action runs, no ack request is sent. Overrides must call the
        /// base implementation to keep the handshake and keep-alive working.</description></item>
        /// </list>
        /// </remarks>
        protected override void OnConnected()
        {
            base.OnConnected();
            BeginAckConnect();
        }

        private void BeginAckConnect()
        {
            _actionQueue.Schedule(() =>
            {
                if (IsLowLevelConnected)
                {
                    _stage = State.AckRequestSent;
                    UnionDataList ackRequest = Memory.CollectablePool.Acquire<UnionDataList>();
                    using var disposer = ackRequest.AsDisposable();
                    try
                    {
                        _handler.FillAckData(ackRequest);
                        ackRequest.PutFirst((byte)MessageType.AckRequestMessage);

                        if (ScheduleToSend(ackRequest.Acquire()) != SendResult.Ok)
                        {
                            Stop(new TextFail(TransportName, "Failed to send ACK"), false);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.wtf(ex);
                        _stage = State.LogicallyDisconnected;
                        Stop(new TextFail(TransportName, "FillAckData throw an exception"), false);
                    }
                }
            });
        }

        private void FinishAckConnect(UnionDataList message)
        {
            _actionQueue.Schedule(() =>
            {
                switch (_stage)
                {
                    case State.AckRequestSent:
                        _stage = State.LogicallyConnected;
                        _handler.OnConnected(this, message);
                        break;

                    case State.Constructed:
                    case State.LogicallyConnected:
                        message.Release();
                        throw new InvalidOperationException($"Wrong RawReliableAckClientEndpoint stage: '{_stage}'");

                    case State.LogicallyDisconnected:
                        message.Release();
                        break;

                    default:
                        throw new ArgumentOutOfRangeException();
                }
            });
        }

        /// <summary>
        /// Handles an acknowledgement response by completing the ack handshake, and delegates all other frame
        /// types to the base implementation.
        /// </summary>
        /// <param name="messageType">The frame type popped from the message.</param>
        /// <param name="message">The frame body. An acknowledgement response is scheduled for handling by this
        /// method.</param>
        /// <returns><see langword="true"/> when the frame was handled; otherwise
        /// <see langword="false"/>.</returns>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Invoked on the caller's thread; the acknowledgement
        /// completion runs on the serial action queue, so handler callbacks remain
        /// serialized.</description></item>
        /// <item><description><b>Other notes</b>: An acknowledgement response received outside the expected
        /// stage is treated as a fatal protocol violation. Overrides must delegate non-acknowledgement frames to
        /// the base implementation.</description></item>
        /// </list>
        /// </remarks>
        protected override bool ProcessInboundMessage(MessageType messageType, UnionDataList message)
        {
            switch (messageType)
            {
                case MessageType.AckResponseMessage:
                    FinishAckConnect(message);
                    return true;
                default:
                    return base.ProcessInboundMessage(messageType, message);
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
            if (_stage == State.LogicallyConnected)
            {
                _stage = State.LogicallyDisconnected;
                _handler.OnDisconnected(reason);
            }
            _stage = State.LogicallyDisconnected;
        }
    }
}