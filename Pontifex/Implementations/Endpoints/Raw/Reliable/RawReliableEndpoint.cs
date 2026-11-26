using System;
using System.Collections.Generic;
using System.Threading;
using Actuarius.Memory;
using Pontifex.StopReasons;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Raw.Reliable
{
    /// <inheritdoc cref="IRawReliableEndpointImp"/>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Other notes</b>: Connection stages are held in volatile fields. Derived classes
    /// supply the transport-specific connect and disconnect work and must drive completion through
    /// FinishConnect and FinishDisconnect.</description></item>
    /// <item><description><b>Handler stop rule</b>: a reliable endpoint that was started delivers the
    /// handler's terminal notification exactly once. The situation decides which callbacks the handler
    /// observes:
    /// <list type="table">
    /// <listheader><term>Situation</term><description>Handler callbacks</description></listheader>
    /// <item><term>The transport's Start returns false</term><description>none</description></item>
    /// <item><term>Start returned true, but the connection failed before OnConnected</term>
    /// <description>exactly one OnStopped</description></item>
    /// <item><term>OnConnected was delivered</term>
    /// <description>OnConnected, then OnDisconnected, then OnStopped, each exactly once</description></item>
    /// <item><term>A server handler was created, but its session endpoint cannot be created, started, or
    /// admitted</term><description>exactly one OnStopped</description></item>
    /// </list>
    /// The terminal notification is once-only even when a stop completion follows a hard
    /// failure.</description></item>
    /// </list>
    /// </remarks>
    public abstract class RawReliableEndpoint : RawEndpoint, IRawReliableEndpointImp
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
            /// A transport handshake request.
            /// </summary>
            HandshakeRequestMessage = 5,

            /// <summary>
            /// A transport handshake response.
            /// </summary>
            HandshakeResponseMessage = 6,

            /// <summary>
            /// A transport handshake rejection.
            /// </summary>
            HandshakeRejectMessage = 7
        }

        private enum State
        {
            Constructed,
            Connecting,
            Connected,
            Disconnecting,
            Disconnected
        }

        private readonly IRawReliableHandler _handler;

        private readonly object _connectGate = new object();
        private readonly System.Collections.Generic.Queue<UnionDataList> _preConnectInbox =
            new System.Collections.Generic.Queue<UnionDataList>();

        private volatile State _currentStage;

        private int _connectedNotified;

        private int _disconnectedNotified;

        private bool _connectCompleted;
        private bool _preConnectReleased;
        private long _preConnectBytes;

        /// <inheritdoc/>
        public bool IsLowLevelConnected => _currentStage == State.Connected;

        /// <summary>
        /// Gets whether the endpoint is logically connected. The default implementation returns
        /// <see cref="IsLowLevelConnected"/> while the logical disconnect notification has not been delivered.
        /// </summary>
        /// <returns><see langword="true"/> when the endpoint is logically connected; otherwise
        /// <see langword="false"/>.</returns>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Safe to read concurrently; overrides must preserve that
        /// property.</description></item>
        /// <item><description><b>Other notes</b>: Overrides may add protocol-specific conditions, such as a
        /// completed handshake. The notification latch makes the predicate false before and during the
        /// handler's logical disconnect notification (see <see cref="IRawReliableHandler.OnDisconnected"/>).</description></item>
        /// </list>
        /// </remarks>
        protected virtual bool GetIsConnected() => IsLowLevelConnected && Volatile.Read(ref _disconnectedNotified) == 0;

        /// <inheritdoc/>
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
            _handler = handler;
            Stopped += OnEndpointStopped;
        }

        /// <summary>
        /// Gets the maximum number of payloads buffered between the handshake and the completion of the
        /// handler's <c>OnConnected</c> callback.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Other notes</b>: Overridable for tests. Reaching the limit terminates the
        /// session with a <see cref="TextFail"/> reason instead of dropping a payload
        /// silently.</description></item>
        /// </list>
        /// </remarks>
        protected virtual int PreConnectBufferMaxMessages => 64;

        /// <summary>
        /// Gets the maximum total payload bytes buffered between the handshake and the completion of the
        /// handler's <c>OnConnected</c> callback.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Other notes</b>: Computed with saturating 64-bit arithmetic from
        /// <see cref="RawEndpoint.MessageMaxByteSize"/>, so a maximum of <see cref="int.MaxValue"/> does not
        /// overflow. Overridable for tests.</description></item>
        /// </list>
        /// </remarks>
        protected virtual long PreConnectBufferMaxBytes =>
            (long)MessageMaxByteSize * PreConnectBufferMaxMessages;

        /// <summary>
        /// Gets the handler that receives inbound messages and connection lifecycle notifications.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Safe to read concurrently; the reference is immutable.</description></item>
        /// <item><description><b>Other notes</b>: The base raw layer retains the same handler instance; this
        /// property only exposes it to the reliable layers that raise the logical notifications.</description></item>
        /// </list>
        /// </remarks>
        protected IRawReliableHandler Handler => _handler;

        /// <summary>
        /// Arms the connected latch so that a later logical disconnect notification is delivered.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Safe to call from any thread; the latch is atomic.</description></item>
        /// <item><description><b>Other notes</b>: Call it only after the handler observed the connection. It does
        /// not notify the handler; <see cref="NotifyConnectedCore"/> both arms the latch and invokes the
        /// callback.</description></item>
        /// </list>
        /// </remarks>
        protected void MarkConnectedNotified()
        {
            Volatile.Write(ref _connectedNotified, 1);
        }

        /// <summary>
        /// Arms the connected latch and invokes the handler's connection callback, isolating a callback
        /// failure.
        /// </summary>
        /// <param name="onConnected">The callback that notifies the handler of the logical connection.</param>
        /// <returns>Always <see langword="true"/>. The value is retained for compatibility; a throwing
        /// callback no longer stops the endpoint.</returns>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Safe to call from any thread.</description></item>
        /// <item><description><b>Handler isolation</b>: a throwing <paramref name="onConnected"/> is logged and
        /// swallowed. The endpoint keeps running and the latch stays armed, so the later logical disconnect
        /// notification is still delivered. A handler exception never stops the endpoint.</description></item>
        /// <item><description><b>Other notes</b>: Implementations that must not deliver a disconnect after a
        /// failed connection arm the latch separately with <see cref="MarkConnectedNotified"/>.</description></item>
        /// </list>
        /// </remarks>
        protected bool NotifyConnectedCore(Action onConnected)
        {
            MarkConnectedNotified();
            try
            {
                onConnected();
            }
            catch (Exception ex)
            {
                Log.wtf(ex);
            }
            finally
            {
                CompleteConnectAndDrainPreConnectInbox();
            }

            return true;
        }

        private void CompleteConnectAndDrainPreConnectInbox()
        {
            lock (_connectGate)
            {
                _connectCompleted = true;

                if (_preConnectReleased || IsStopRequested)
                {
                    // A stop requested before or during the start notification releases every buffered
                    // payload without delivering it.
                    while (_preConnectInbox.Count > 0)
                    {
                        _preConnectInbox.Dequeue().Release();
                    }

                    _preConnectBytes = 0;
                    return;
                }

                while (_preConnectInbox.Count > 0)
                {
                    UnionDataList message = _preConnectInbox.Dequeue();
                    _preConnectBytes -= message.GetDataSize();
                    base.ProcessInboundMessage(message);
                }

                _preConnectBytes = 0;
            }
        }

        private void ReleasePreConnectInbox()
        {
            List<UnionDataList>? released = null;

            lock (_connectGate)
            {
                _preConnectReleased = true;

                if (_preConnectInbox.Count > 0)
                {
                    released = new List<UnionDataList>(_preConnectInbox.Count);
                    while (_preConnectInbox.Count > 0)
                    {
                        released.Add(_preConnectInbox.Dequeue());
                    }
                }

                _preConnectBytes = 0;
            }

            if (released != null)
            {
                foreach (UnionDataList message in released)
                {
                    message.Release();
                }
            }
        }

        private void OnEndpointStopped(StopReason reason)
        {
            ReleasePreConnectInbox();
        }

        /// <summary>
        /// Delivers the handler's logical disconnection notification at most once, and only after the handler
        /// observed the connection.
        /// </summary>
        /// <param name="reason">The reason passed to the handler.</param>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Safe to call from any thread; the one-shot latch is
        /// atomic.</description></item>
        /// <item><description><b>Other notes</b>: The same latch reports the disconnected state read by
        /// <see cref="GetIsConnected"/>, so IsConnected is false before and during the handler's notification. A
        /// handler exception is logged and swallowed.</description></item>
        /// </list>
        /// </remarks>
        protected void NotifyDisconnectedCore(StopReason reason)
        {
            if (Interlocked.Exchange(ref _disconnectedNotified, 1) == 0 && Volatile.Read(ref _connectedNotified) != 0)
            {
                try
                {
                    _handler.OnDisconnected(reason);
                }
                catch (Exception ex)
                {
                    Log.wtf(ex);
                }
            }
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
        /// delivers the handler's stop notification at most once, from the starting, started, or stopping
        /// stage; a constructed or already stopped endpoint stays silent. The handler receives no disconnect
        /// notification on this path.</description></item>
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

        /// <inheritdoc cref="IRawEndpoint.Send"/>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Precedence</b>: endpoint state is checked first, so a stopped endpoint
        /// reports the state result even for a null, released, or oversized buffer. A running endpoint then
        /// reports <see cref="SendResult.InvalidMessage"/> for a null or released buffer and
        /// <see cref="SendResult.MessageTooBig"/> for an oversized one.</description></item>
        /// <item><description><b>Ownership</b>: on a state rejection a non-null live buffer is released; a
        /// null or dead buffer stays with the caller, as for every
        /// <see cref="SendResult.InvalidMessage"/>.</description></item>
        /// </list>
        /// </remarks>
        public override SendResult Send(UnionDataList bufferToSend)
        {
            if (!CanSendNow(out var errorCode))
            {
                if (bufferToSend != null! && bufferToSend.IsAlive)
                {
                    bufferToSend.Release();
                }
                return errorCode;
            }

            if (bufferToSend == null! || !bufferToSend.IsAlive)
            {
                return SendResult.InvalidMessage;
            }

            int payloadSize = bufferToSend.GetDataSize();
            if (payloadSize > MessageMaxByteSize)
            {
                bufferToSend.Release();
                return SendResult.MessageTooBig;
            }

            bufferToSend.PutFirst((byte)MessageType.PayloadMessage);
            return ScheduleToSend(bufferToSend, payloadSize);
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
                    return DeliverPayload(message);
                case MessageType.DisconnectMessage:
                    message.Release();
                    Stop(new GracefulRemoteIntention(TransportName), false);
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Delivers or buffers one inbound payload according to the connect-completed latch.
        /// </summary>
        /// <param name="message">The payload body. Ownership transfers to this method.</param>
        /// <returns>Always <see langword="true"/>; the payload is either delivered, buffered, or released by
        /// this method.</returns>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Other notes</b>: A payload that arrives before the handler's
        /// <c>OnConnected</c> completed is buffered in arrival order and drained immediately after the
        /// callback, so it is delivered exactly once and in order rather than dropped. A payload that cannot
        /// fit the pre-connect caps releases every buffered payload and the triggering payload, then stops
        /// the session with a <see cref="TextFail"/> reason; it is never dropped silently.</description></item>
        /// </list>
        /// </remarks>
        private bool DeliverPayload(UnionDataList message)
        {
            bool overflow = false;

            lock (_connectGate)
            {
                if (_connectCompleted)
                {
                    base.ProcessInboundMessage(message);
                    return true;
                }

                if (_preConnectReleased || IsStopRequested)
                {
                    message.Release();
                    return true;
                }

                int size = message.GetDataSize();
                if (_preConnectInbox.Count >= PreConnectBufferMaxMessages ||
                    _preConnectBytes + size > PreConnectBufferMaxBytes)
                {
                    overflow = true;
                    while (_preConnectInbox.Count > 0)
                    {
                        _preConnectInbox.Dequeue().Release();
                    }

                    _preConnectBytes = 0;
                    message.Release();
                }
                else
                {
                    _preConnectInbox.Enqueue(message);
                    _preConnectBytes += size;
                }
            }

            if (overflow)
            {
                Log.e("Pre-connect payload buffer exceeded; the session is stopping");
                Stop(new TextFail(TransportName, "Pre-connect payload buffer exceeded"), false);
            }

            return true;
        }

        /// <summary>
        /// Validates the total framed size of an inbound handshake frame.
        /// </summary>
        /// <param name="messageType">The frame type that was popped.</param>
        /// <param name="totalSize">The complete framed size, measured before the frame type was popped.</param>
        /// <returns><see langword="true"/> when the frame is admissible; otherwise
        /// <see langword="false"/>.</returns>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Other notes</b>: The default returns <see langword="true"/>. A handshake client
        /// overrides it to enforce <c>HandshakeMaxByteSize</c> on handshake responses and rejects, including the
        /// frame type. A normal payload above 128 bytes must never be rejected by this hook.</description></item>
        /// </list>
        /// </remarks>
        protected virtual bool ValidateInboundHandshakeFrame(MessageType messageType, int totalSize)
        {
            return true;
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
            if (!IsLowLevelConnected)
            {
                // A frame delivered after the connection ended is not malformed; it is simply late.
                message.Release();
                return;
            }

            int totalSize = message.GetDataSize();

            if (!message.TryPopFirst(out byte msgType))
            {
                message.Release();
                Log.e("Inbound reliable frame had no frame type and was discarded");
                return;
            }

            MessageType frameType = (MessageType)msgType;
            if (!ValidateInboundHandshakeFrame(frameType, totalSize))
            {
                message.Release();
                Stop(new ProtocolMismatch(TransportName,
                    "Handshake frame exceeds HandshakeMaxByteSize"), false);
                return;
            }

            if (message.GetDataSize() > MessageMaxByteSize)
            {
                message.Release();
                Log.e("Inbound reliable payload exceeds MessageMaxByteSize and was discarded");
                return;
            }

            if (!ProcessInboundMessage(frameType, message))
            {
                message.Release();
                Log.e("Inbound reliable frame type '{0}' is not handled and was discarded", msgType);
            }
        }
    }
}
