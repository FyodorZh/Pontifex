using System;
using Actuarius.Memory;
using Pontifex.StopReasons;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Raw.Reliable
{
    /// <summary>
    /// Abstract base class for a reliable raw endpoint. Extends <see cref="RawEndpoint"/> with a logical
    /// connection stage machine: connection completion, graceful and ungraceful disconnection, framing of
    /// outbound payloads, and dispatch of inbound control frames.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Thread safety</b>: All public and protected instance members are safe to call
    /// concurrently from any thread. Connection stages, handler dispatch, and payload transmission are
    /// serialized by the inherited action queue; the stage fields are volatile.</description></item>
    /// <item><description><b>Preconditions/Postconditions</b>: The lifecycle is one-shot. After the endpoint
    /// reaches the disconnected or stopped state it cannot be connected again.</description></item>
    /// <item><description><b>Other notes</b>: Derived classes supply the transport-specific connect and
    /// disconnect work and must drive completion through FinishConnect and FinishDisconnect.</description></item>
    /// </list>
    /// </remarks>
    public abstract class RawReliableEndpoint : RawEndpoint, IRawReliableEndpoint
    {
        /// <summary>
        /// Identifies the leading control byte that frames every inbound and outbound message.
        /// </summary>
        protected enum MessageType : byte
        {
            /// <summary>
            /// Not a valid frame type; treated as a parse failure.
            /// </summary>
            InvalidMessage = 0,

            /// <summary>
            /// An application payload frame.
            /// </summary>
            PayloadMessage = 1,

            /// <summary>
            /// A graceful disconnect request from the remote peer.
            /// </summary>
            DisconnectMessage = 2,

            /// <summary>
            /// A keep-alive ping.
            /// </summary>
            PingMessage = 3,

            /// <summary>
            /// A keep-alive reply.
            /// </summary>
            PongMessage = 4,

            /// <summary>
            /// An acknowledgement request.
            /// </summary>
            AckRequestMessage = 5,

            /// <summary>
            /// An acknowledgement response.
            /// </summary>
            AckResponseMessage = 6
        }

        private enum State
        {
            Constructed,
            Connecting,
            Connected,
            Disconnecting,
            Disconnected
        }

        private volatile State _currentStage;

        /// <summary>
        /// Gets a value indicating whether the low-level connection has completed and disconnection has not
        /// yet begun.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Safe to read concurrently.</description></item>
        /// <item><description><b>Other notes</b>: Becomes true only after FinishConnect completes and becomes
        /// false as soon as disconnection begins, before the handler's disconnect notification is
        /// raised.</description></item>
        /// </list>
        /// </remarks>
        public bool IsLowLevelConnected => _currentStage == State.Connected;

        /// <summary>
        /// Gets whether the endpoint is logically connected. The default implementation returns
        /// <see cref="IsLowLevelConnected"/>.
        /// </summary>
        /// <returns><see langword="true"/> when the endpoint is logically connected; otherwise
        /// <see langword="false"/>.</returns>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Safe to read concurrently; overrides must preserve that
        /// property.</description></item>
        /// <item><description><b>Other notes</b>: Overrides may add protocol-specific conditions, such as a
        /// completed handshake.</description></item>
        /// </list>
        /// </remarks>
        protected virtual bool GetIsConnected() => IsLowLevelConnected;

        /// <summary>
        /// Gets a value indicating whether the endpoint is currently logically connected.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Safe to read concurrently.</description></item>
        /// <item><description><b>Other notes</b>: May return true before OnConnected is invoked. Guaranteed to
        /// return false during and after the handler's disconnect notification, because the transition to
        /// false is synchronized with that call.</description></item>
        /// </list>
        /// </remarks>
        public bool IsConnected => GetIsConnected();

        /// <summary>
        /// Initializes a new reliable endpoint owned by the given transport.
        /// </summary>
        /// <param name="transportName">The name of the transport that owns this endpoint.</param>
        /// <param name="remoteEndPoint">The remote endpoint address, or <see langword="null"/> when it is
        /// unknown.</param>
        /// <param name="handler">The handler that receives inbound messages and connection lifecycle
        /// notifications.</param>
        /// <param name="messageMaxByteSize">The inclusive maximum message size, in bytes, admitted by this
        /// endpoint; must be at least 1.</param>
        /// <param name="bufferCapacityBytes">The byte budget for messages accepted by this endpoint but not
        /// yet transmitted; must be at least 1.</param>
        /// <param name="memory">The memory rental system used to rent buffers.</param>
        /// <param name="log">The logging system used by this endpoint.</param>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Other notes</b>: The owning transport and the handler are retained, not
        /// disposed, by the endpoint. The endpoint starts in the constructed stage.</description></item>
        /// </list>
        /// </remarks>
        protected RawReliableEndpoint(
            string transportName, IEndPoint remoteEndPoint, IRawReliableHandler handler, 
            int messageMaxByteSize, long bufferCapacityBytes, IMemoryRental memory, ILogger log)
            : base(transportName, remoteEndPoint, handler, messageMaxByteSize, bufferCapacityBytes, memory, log)
        {
        }

        /// <summary>
        /// Handles a fatal endpoint failure by moving the reliable stage to disconnected and then invoking the
        /// base implementation.
        /// </summary>
        /// <param name="reason">The error description that caused the failure.</param>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Invoked on the thread that executed the failing
        /// action.</description></item>
        /// <item><description><b>Other notes</b>: The base implementation transitions the stage to Stopped and
        /// delivers the handler's stop notification when the endpoint had started. The handler receives no
        /// disconnect notification on this path.</description></item>
        /// </list>
        /// </remarks>
        protected override void FailHard(StopReason reason)
        {
            _currentStage = State.Disconnected;
            base.FailHard(reason);
        }

        /// <summary>
        /// Transitions the reliable stage from constructed to connecting and invokes BeginConnect.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Called on the serial action queue.</description></item>
        /// <item><description><b>Preconditions/Postconditions</b>: The stage must be constructed.</description></item>
        /// </list>
        /// </remarks>
        protected sealed override void BeginStart()
        {
            if (_currentStage != State.Constructed)
            {
                throw new InvalidOperationException($"Stage expected to be 'Constructed' but was {_currentStage}");
            }

            _currentStage = State.Connecting;
            BeginConnect();
        }

        /// <summary>
        /// Begins the transport-specific connection attempt. Invoked on the serial action queue when the
        /// reliable stage becomes connecting.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Preconditions/Postconditions</b>: The implementation must not throw and must
        /// eventually call FinishConnect, either synchronously or later from any thread.</description></item>
        /// </list>
        /// </remarks>
        protected abstract void BeginConnect();
        
        /// <summary>
        /// Completes the connection attempt. Invokes the base start completion and, on success, moves the
        /// reliable stage to connected and invokes OnConnected.
        /// </summary>
        /// <param name="ifFailed">A callback that releases resources allocated by the connection attempt when
        /// it cannot complete. It is invoked on the serial action queue and must not throw.</param>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Safe to call from any thread; the transition is serialized
        /// on the serial action queue.</description></item>
        /// <item><description><b>Preconditions/Postconditions</b>: Intended to be called exactly once per
        /// endpoint by BeginConnect or its continuation. If a stop was requested while connecting, the
        /// connection is still reported and is then torn down.</description></item>
        /// </list>
        /// </remarks>
        protected void FinishConnect(Action ifFailed)
        {
            bool isOK = true;
            FinishStart(() => isOK = false);
            _actionQueue.Schedule(() =>
            {
                if (!isOK)
                {
                    ifFailed();
                    return;
                }

                switch (_currentStage)
                {
                    case State.Connecting:
                        _currentStage = State.Connected;
                        OnConnected();
                        break;
                    case State.Constructed:
                    case State.Connected:
                        throw new InvalidOperationException($"RawReliableEndpoint.Stage impossible value '{_currentStage}'");
                    case State.Disconnecting:
                    case State.Disconnected:
                        ifFailed();
                        _currentStage = State.Disconnected;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            });
        }
        
        /// <summary>
        /// Invoked once when the reliable stage becomes connected. The base implementation does nothing.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Called on the serial action queue, serialized with inbound
        /// delivery and stop notification. An exception fails the endpoint hard.</description></item>
        /// </list>
        /// </remarks>
        protected virtual void OnConnected() {}

        /// <summary>
        /// Performs the reliable part of the stop transition: completes the stop directly when not connected,
        /// or notifies the handler and begins disconnection when connected.
        /// </summary>
        /// <param name="reason">The reason the endpoint is being stopped.</param>
        /// <param name="tryBeGentle">A hint forwarded to BeginDisconnect indicating whether a graceful
        /// disconnection should be attempted.</param>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Called on the serial action queue.</description></item>
        /// <item><description><b>Other notes</b>: When the stage is connected, the handler's disconnect
        /// notification is raised before BeginDisconnect is invoked.</description></item>
        /// </list>
        /// </remarks>
        protected sealed override void BeginStop(StopReason reason, bool tryBeGentle)
        {
            _actionQueue.Schedule(() =>
            {
                switch (_currentStage)
                {
                    case State.Constructed:
                        _currentStage = State.Disconnected;
                        FinishStop();
                        break;
                    case State.Connecting:
                        _currentStage = State.Disconnected;
                        FinishStop();
                        break;
                    case State.Connected:
                        _currentStage = State.Disconnecting;
                        BeginDisconnect(reason, tryBeGentle);
                        break;
                    case State.Disconnected:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            });
        }

        /// <summary>
        /// Begins the transport-specific disconnection. Invoked on the serial action queue only when the
        /// endpoint was connected.
        /// </summary>
        /// <param name="reason">The reason the endpoint is being stopped.</param>
        /// <param name="tryBeGentle">A hint indicating whether a graceful disconnection should be
        /// attempted.</param>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Preconditions/Postconditions</b>: The implementation must not throw and must
        /// eventually call FinishDisconnect, either synchronously or later from any thread.</description></item>
        /// </list>
        /// </remarks>
        protected abstract void BeginDisconnect(StopReason reason, bool tryBeGentle);

        /// <summary>
        /// Completes the disconnection. Moves the reliable stage to disconnected and completes the stop
        /// transition. Invoked from the transport-specific <see cref="BeginDisconnect"/> implementation.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Safe to call from any thread; the transition is serialized
        /// on the serial action queue.</description></item>
        /// <item><description><b>Preconditions/Postconditions</b>: Intended to be called exactly once per
        /// endpoint by BeginDisconnect or its continuation, while the stage is disconnecting.</description></item>
        /// </list>
        /// </remarks>
        protected void FinishDisconnect()
        {
            _actionQueue.Schedule(() =>
            {
                if (_currentStage != State.Disconnecting)
                {
                    throw new InvalidOperationException($"Expected current state to be 'Disconnecting' but observed '{_currentStage}'");
                }

                _currentStage = State.Disconnected;
                FinishStop();
            });
        }

        /// <inheritdoc/>
        bool IRawReliableEndpoint.Disconnect(StopReason reason)
        {
            Stop(reason, true);
            return true;
        }

        /// <summary>
        /// Determines whether the endpoint can currently admit a new outbound message.
        /// </summary>
        /// <param name="errorCode">When the method returns false, receives the SendResult that describes why
        /// sending is not allowed; otherwise it is set to SendResult.Ok.</param>
        /// <returns><see langword="true"/> when a message may be admitted; otherwise <see langword="false"/>
        /// with errorCode set.</returns>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Safe to call concurrently; the observed state is a
        /// best-effort snapshot.</description></item>
        /// <item><description><b>Other notes</b>: Rejects with SendResult.NotConnected unless the reliable
        /// stage is connected; the base implementation additionally rejects when a stop has been
        /// requested.</description></item>
        /// </list>
        /// </remarks>
        protected override bool CanSendNow(out SendResult errorCode)
        {
            if (_currentStage != State.Connected)
            {
                errorCode = SendResult.NotConnected;
                return false;
            }
            
            return base.CanSendNow(out errorCode);
        }

        /// <summary>
        /// Sends a payload message to the remote peer, framing it with the payload message type.
        /// </summary>
        /// <param name="bufferToSend">The payload to send. Ownership transfers to the endpoint on every result
        /// except InvalidMessage; on InvalidMessage the buffer is null or not alive and cannot be transferred,
        /// so the caller retains ownership.</param>
        /// <returns>A SendResult describing the admission: Ok, or a synchronous rejection.</returns>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Safe for concurrent calls from any thread; concurrent Ok
        /// sends are transmitted in admission order.</description></item>
        /// <item><description><b>Side effects</b>: The frame type byte is prepended to the buffer before
        /// admission, so the buffer is modified even when admission later fails.</description></item>
        /// </list>
        /// </remarks>
        public override SendResult Send(UnionDataList bufferToSend)
        {
            if (bufferToSend != null! && bufferToSend.IsAlive)
            {
                bufferToSend.PutFirst((byte)MessageType.PayloadMessage);
                return ScheduleToSend(bufferToSend);
            }
            return SendResult.InvalidMessage;
        }

        /// <summary>
        /// Sends a graceful disconnect message and stops the endpoint. Intended to be called by derived
        /// classes when the local side initiates a graceful shutdown.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Safe to call from any thread.</description></item>
        /// <item><description><b>Side effects</b>: The endpoint is stopped with a UserIntention reason; the
        /// stop is attempted gently only if the disconnect message was admitted.</description></item>
        /// </list>
        /// </remarks>
        protected void DisconnectGracefully()
        {
            var buffer = Memory.CollectablePool.Acquire<UnionDataList>();
            buffer.PutFirst((byte)MessageType.DisconnectMessage);
            var res = ScheduleToSend(buffer);
            Stop(new UserIntention(TransportName), res == SendResult.Ok);
        }

        /// <summary>
        /// Dispatches one inbound frame by its message type. The base implementation delivers payload frames
        /// to the handler and treats a disconnect frame as a remote graceful disconnect.
        /// </summary>
        /// <param name="messageType">The frame type popped from the message.</param>
        /// <param name="message">The frame body. When this method returns <see langword="true"/> the override
        /// has taken ownership of the message and must have delivered or released it; when it returns
        /// <see langword="false"/> the caller releases the message and stops the endpoint.</param>
        /// <returns><see langword="true"/> when the frame was handled; otherwise <see langword="false"/>.</returns>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Invoked on the caller's thread; overrides must be safe for
        /// concurrent invocation if inbound delivery can be concurrent.</description></item>
        /// </list>
        /// </remarks>
        protected virtual bool ProcessInboundMessage(MessageType messageType, UnionDataList message)
        {
            switch (messageType)
            {
                case MessageType.PayloadMessage:
                    base.ProcessInboundMessage(message);
                    return true;
                case MessageType.DisconnectMessage:
                    message.Release();
                    Stop(new GracefulRemoteIntention(TransportName), false);
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Handles one inbound message by popping its frame type and dispatching it, releasing the message and
        /// stopping the endpoint when the frame cannot be handled.
        /// </summary>
        /// <param name="message">The received message. Ownership transfers to the endpoint.</param>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Safe to call from any thread; the dispatch itself runs on
        /// the caller's thread.</description></item>
        /// <item><description><b>Preconditions/Postconditions</b>: The message must not be
        /// <see langword="null"/>. It is released exactly once, either by the dispatch or by this
        /// method.</description></item>
        /// </list>
        /// </remarks>
        protected sealed override void ProcessInboundMessage(UnionDataList message)
        {
            if (!IsLowLevelConnected ||
                !message.TryPopFirst(out byte msgType) ||
                !ProcessInboundMessage((MessageType)msgType, message))
            {
                message.Release();
                Stop(new TextFail(TransportName, "Failed to parse incoming message"), false);
            }
        }
    }
}
