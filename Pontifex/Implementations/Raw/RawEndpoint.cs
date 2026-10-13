using System;
using System.Collections.Generic;
using System.Threading;
using Actuarius.Memory;
using Pontifex.StopReasons;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Raw
{
    /// <summary>
    /// Abstract base class for a raw data endpoint. Provides thread-safe outbound message admission and
    /// serialization through an internal action queue, inbound message dispatch to the handler, and the
    /// one-shot start and stop lifecycle shared by raw transports.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Thread safety</b>: All public and protected instance members are safe to call
    /// concurrently from any thread. State transitions and handler dispatch are serialized by an internal
    /// action queue that runs one action at a time; the executing thread is not fixed. Outbound messages are
    /// transmitted in admission order.</description></item>
    /// <item><description><b>Preconditions/Postconditions</b>: The lifecycle is one-shot. A stop request is
    /// latched, and after the endpoint has stopped it cannot be started again.</description></item>
    /// <item><description><b>Other notes</b>: An exception that escapes a queued action fails the endpoint
    /// hard: it is logged, the stop reason is set to an ExceptionFail, and FailHard is invoked. The base
    /// implementation of FailHard delivers the handler's stop notification when the endpoint had started and
    /// then transitions the stage to Stopped; derived classes override it to release resources.</description></item>
    /// </list>
    /// </remarks>
    public abstract class RawEndpoint : IRawEndpoint
    {
        private readonly long _bufferCapacityBytes;
        private readonly IRawHandler _handler;

        /// <summary>
        /// The serial action queue that executes all endpoint state transitions and handler dispatch, one
        /// action at a time.
        /// </summary>
        protected readonly ISerialActionQueue _actionQueue;

        private long _totalBuffersSize;

        private enum State
        {
            Constructed = 0,
            Starting,
            Started,
            Stopping,
            Stopped
        }

        private volatile State _currentStage = State.Constructed;
        private volatile StopReason? _stopReason;

        private readonly Action<UnionDataList> _sendAction;
        private readonly Action<UnionDataList> _receiveAction;

        /// <summary>
        /// Gets the remote endpoint address, or <see langword="null"/> when no remote address is known.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Safe to read concurrently.</description></item>
        /// <item><description><b>Other notes</b>: The value is captured at construction and never changes for
        /// the lifetime of the endpoint, including after a stop.</description></item>
        /// </list>
        /// </remarks>
        public IEndPoint? RemoteEndPoint { get; }

        /// <summary>
        /// Gets the maximum message size in bytes supported by the transport. It is an inclusive maximum for
        /// the application payload and excludes transport framing and control metadata; empty payloads are
        /// valid.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Safe to read concurrently.</description></item>
        /// <item><description><b>Other notes</b>: The value is fixed at construction. Admission compares the
        /// size of a message to be sent against this limit.</description></item>
        /// </list>
        /// </remarks>
        public int MessageMaxByteSize { get; }

        /// <summary>
        /// Gets the name of the transport that owns this endpoint.
        /// </summary>
        protected string TransportName { get; }

        /// <summary>
        /// Gets the logging system used by this endpoint.
        /// </summary>
        protected ILogger Log { get; }

        /// <summary>
        /// Gets the memory rental system used to rent buffers.
        /// </summary>
        protected IMemoryRental Memory { get; }

        /// <summary>
        /// Gets a value indicating whether the endpoint is running and accepting new work, meaning the stage
        /// is Started and no stop has been requested.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Safe to read concurrently.</description></item>
        /// <item><description><b>Other notes</b>: Becomes false as soon as a stop is requested, even while
        /// work already accepted may still be draining. Derived classes expose transport-specific validity
        /// getters in terms of this property.</description></item>
        /// </list>
        /// </remarks>
        public bool IsRunning => _currentStage == State.Started && _stopReason == null;

        /// <summary>
        /// Initializes a new raw endpoint with the transport name, logging, and memory rental supplied
        /// explicitly by the caller.
        /// </summary>
        /// <param name="transportName">The name of the transport that owns this endpoint.</param>
        /// <param name="remoteEndpoint">The remote endpoint address, or <see langword="null"/> when it is
        /// unknown.</param>
        /// <param name="handler">The handler that receives inbound messages and stop notifications.</param>
        /// <param name="messageMaxByteSize">The inclusive maximum message size, in bytes, admitted by this
        /// endpoint; must be at least 1.</param>
        /// <param name="bufferCapacityBytes">The byte budget for messages accepted by this endpoint but not
        /// yet transmitted; must be at least 1.</param>
        /// <param name="memory">The memory rental system used to rent buffers.</param>
        /// <param name="log">The logging system used by this endpoint.</param>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Preconditions/Postconditions</b>: messageMaxByteSize and bufferCapacityBytes
        /// must each be at least 1.</description></item>
        /// <item><description><b>Other notes</b>: The logging system and the handler are expected to be
        /// non-null and are retained, not disposed, by the endpoint.</description></item>
        /// </list>
        /// </remarks>
        protected RawEndpoint(string transportName, IEndPoint remoteEndpoint,
            IRawHandler handler, int messageMaxByteSize, long bufferCapacityBytes, IMemoryRental memory, ILogger log)
        {
            if (bufferCapacityBytes < 1)
            {
                throw new ArgumentException(nameof(bufferCapacityBytes));
            }

            if (messageMaxByteSize < 1)
            {
                throw new ArgumentException(nameof(messageMaxByteSize));
            }

            RemoteEndPoint = remoteEndpoint;
            MessageMaxByteSize = messageMaxByteSize;

            TransportName = transportName;
            Log = log;
            Memory = memory;
            _bufferCapacityBytes = bufferCapacityBytes;
            _handler = handler;

            _actionQueue = SerialQueueConstructor(ex =>
            {
                Log.wtf(ex);
                _stopReason = new ExceptionFail(TransportName, ex, "Unhandled exception");
                FailHard(_stopReason);
            });

            _sendAction = msg =>
            {
                int size = msg.GetDataSize();
                try
                {
                    if (_currentStage == State.Started)
                    {
                        DoSendRaw(msg);
                    }
                    else
                    {
                        msg.Release();
                    }
                }
                finally
                {
                    Interlocked.Add(ref _totalBuffersSize, -size);
                }
            };

            _receiveAction = msg =>
            {
                if (_currentStage == State.Started)
                {
                    _handler.OnReceived(msg);
                }
                else
                {
                    msg.Release();
                }
            };
        }

        /// <summary>
        /// Creates the serial action queue that executes all endpoint transitions and handler dispatch.
        /// </summary>
        /// <param name="onError">The handler invoked with exceptions that escape a queued action. It must be
        /// non-null and must not throw.</param>
        /// <returns>The serial action queue used by this endpoint.</returns>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Other notes</b>: The default implementation returns an unbounded
        /// <see cref="SerialActionQueue"/>. It is invoked exactly once, during construction, and derived classes
        /// override it to supply a custom queue. An override must not depend on derived state that is not yet
        /// initialized, because the call originates from the base constructor.</description></item>
        /// </list>
        /// </remarks>
        protected virtual ISerialActionQueue SerialQueueConstructor(Action<Exception> onError)
        {
            return new SerialActionQueue(onError);
        }

        /// <summary>
        /// Handles a fatal endpoint failure. Invoked after the stop reason has been recorded. The base
        /// implementation delivers the handler's stop notification when the endpoint had started and then
        /// transitions the stage to Stopped; derived classes override to release unmanaged resources and
        /// terminate their work.
        /// </summary>
        /// <param name="reason">The error description that caused the failure.</param>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Invoked on the thread that executed the failing action;
        /// the base implementation delivers the stop notification and sets the stage to
        /// Stopped.</description></item>
        /// <item><description><b>Preconditions/Postconditions</b>: The base implementation delivers the stop
        /// notification only when the stage is Started, then sets the stage to Stopped, and the override must
        /// not throw.</description></item>
        /// </list>
        /// </remarks>
        protected virtual void FailHard(StopReason reason)
        {
            if (_currentStage == State.Started)
            {
                _handler.OnStopped(reason);
            }
            _currentStage = State.Stopped;
        }

        #region Start

        /// <summary>
        /// Requests a start of the endpoint, scheduling BeginStart on the endpoint's serial action queue. May
        /// be called at most once, before any stop.
        /// </summary>
        /// <returns>true if the start request was accepted for processing; otherwise false. A successful
        /// return means the request was queued, not that the endpoint has started.</returns>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Safe to call from any thread; the transition is serialized
        /// on the internal action queue.</description></item>
        /// <item><description><b>Preconditions/Postconditions</b>: Legal only while the endpoint is in the
        /// constructed stage. A redundant call fails the endpoint hard.</description></item>
        /// <item><description><b>Other notes</b>: Observe OnStarted or IsRunning to learn when the endpoint is
        /// actually running.</description></item>
        /// </list>
        /// </remarks>
        public bool Start()
        {
            return _actionQueue.Schedule(() =>
            {
                if (_currentStage != State.Constructed)
                {
                    throw new InvalidOperationException($"Stage expected to be 'Constructed' but was {_currentStage}");
                }
                _currentStage = State.Starting;
                BeginStart();
            });
        }

        /// <summary>
        /// Performs the transport-specific start work. Invoked on the serial action queue after the endpoint
        /// enters the starting stage.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Called on the serial action queue; never concurrently with
        /// another transition.</description></item>
        /// <item><description><b>Preconditions/Postconditions</b>: The implementation must call FinishStart
        /// exactly once, either synchronously or later from any thread, passing a callback that releases
        /// resources allocated during the start attempt. An exception fails the endpoint hard.</description></item>
        /// </list>
        /// </remarks>
        protected abstract void BeginStart();

        /// <summary>
        /// Completes the start transition, invoking OnStarted when the endpoint is still starting.
        /// </summary>
        /// <param name="onFail">A callback that releases resources allocated by BeginStart when the start
        /// cannot complete. It is invoked on the serial action queue and must not throw.</param>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: The transition is serialized on the internal action queue;
        /// the call may be scheduled from any thread.</description></item>
        /// <item><description><b>Preconditions/Postconditions</b>: Intended to be called exactly once per
        /// endpoint by BeginStart or its continuation. When the stage is Starting the endpoint becomes Started
        /// and OnStarted is invoked; if a stop was already requested, the endpoint then transitions to
        /// Stopping and BeginStop is invoked. Any other stage invokes onFail and fails the endpoint
        /// hard.</description></item>
        /// </list>
        /// </remarks>
        protected void FinishStart(Action onFail)
        {
            _actionQueue.Schedule(() =>
            {
                switch (_currentStage)
                {
                    case State.Starting:
                        _currentStage = State.Started;
                        
                        if (_stopReason != null)
                        {
                            _currentStage = State.Stopping;
                            BeginStop(_stopReason, false);
                        }
                        break;

                    case State.Constructed:
                    case State.Started:
                    case State.Stopping:
                    case State.Stopped:
                        onFail();
                        throw new InvalidOperationException($"Invalid RawEndpoint.Stage {_currentStage}");

                    default:
                        throw new ArgumentOutOfRangeException();
                }
            });
        }

        #endregion

        #region Work

        /// <summary>
        /// Delivers one inbound message to the handler, or drops it when the endpoint is not running.
        /// </summary>
        /// <param name="message">The received message. On delivery, ownership transfers to the handler; on
        /// drop, the endpoint releases it.</param>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Safe to call from any number of threads; delivery is
        /// serialized with the stop notification, so a message is never delivered after the stop
        /// notification.</description></item>
        /// <item><description><b>Preconditions/Postconditions</b>: The message must not be
        /// <see langword="null"/>. It is released exactly once when the endpoint is not running or the message
        /// cannot be scheduled.</description></item>
        /// </list>
        /// </remarks>
        protected virtual void ProcessInboundMessage(UnionDataList message)
        {
            if (!IsRunning)
            {
                message.Release();
            }
            else if (!_actionQueue.Schedule(message, _receiveAction, msg => msg.Release()))
            {
                message.Release();
            }
        }

        /// <summary>
        /// Determines whether the endpoint can currently admit a new outbound message.
        /// </summary>
        /// <param name="errorCode">When the method returns false, receives the SendResult that describes why
        /// sending is not allowed; otherwise it is set to SendResult.Ok.</param>
        /// <returns>true when a message may be admitted; otherwise false with errorCode set.</returns>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Safe to call concurrently; the observed state is a
        /// best-effort snapshot.</description></item>
        /// <item><description><b>Other notes</b>: The default implementation rejects with SendResult.NotConnected
        /// when a stop has been requested or the endpoint is not in the started stage. Overrides add
        /// transport-specific flow control and must not throw.</description></item>
        /// </list>
        /// </remarks>
        protected virtual bool CanSendNow(out SendResult errorCode)
        {
            errorCode = SendResult.Ok;

            if (_stopReason != null)
            {
                errorCode = SendResult.NotConnected;
            }
            else if (_currentStage != State.Started)
            {
                errorCode = SendResult.NotConnected;
            }

            return errorCode == SendResult.Ok;
        }

        /// <summary>
        /// Validates and admits one outbound buffer for transmission, or rejects it synchronously.
        /// </summary>
        /// <param name="bufferToSend">The buffer to send. Ownership transfers to the endpoint on every result
        /// except InvalidMessage; on InvalidMessage the buffer is null or not alive and cannot be transferred,
        /// so the caller retains ownership.</param>
        /// <returns>A SendResult describing the admission: Ok, or a synchronous rejection.</returns>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Safe for concurrent calls from any thread; concurrent Ok
        /// sends are transmitted in admission order.</description></item>
        /// <item><description><b>Preconditions/Postconditions</b>: On any result other than InvalidMessage the
        /// endpoint consumes and releases the buffer. On Ok the endpoint owns the buffer and releases it after
        /// transmission or when it is dropped by a stop. The byte budget is a best-effort bound under
        /// concurrent senders.</description></item>
        /// <item><description><b>Other notes</b>: A successful admission requests processing of the internal
        /// action queue.</description></item>
        /// </list>
        /// </remarks>
        protected SendResult ScheduleToSend(UnionDataList bufferToSend)
        {
            if (bufferToSend == null! || !bufferToSend.IsAlive)
            {
                return SendResult.InvalidMessage;
            }

            int bufferSize = bufferToSend.GetDataSize();
            if (bufferSize > MessageMaxByteSize)
            {
                bufferToSend.Release();
                return SendResult.MessageTooBig;
            }

            if (!CanSendNow(out var errorCode))
            {
                bufferToSend.Release();
                return errorCode;
            }

            Interlocked.Add(ref _totalBuffersSize, bufferSize);
            if (_totalBuffersSize > _bufferCapacityBytes)
            {
                Interlocked.Add(ref _totalBuffersSize, -bufferSize);
                bufferToSend.Release();
                return SendResult.BufferOverflow;
            }

            if (!_actionQueue.Schedule(bufferToSend, _sendAction, msg =>
                {
                    Interlocked.Add(ref _totalBuffersSize, -msg.GetDataSize());
                    msg.Release();
                }))
            {
                Interlocked.Add(ref _totalBuffersSize, -bufferSize);
                bufferToSend.Release();
                return SendResult.Error;
            }

            return SendResult.Ok;
        }

        /// <inheritdoc/>
        public virtual SendResult Send(UnionDataList bufferToSend)
        {
            return ScheduleToSend(bufferToSend);
        }

        #endregion

        #region Stop

        /// <summary>
        /// Requests a stop of the endpoint and latches the given reason. Only the first call performs the
        /// transition; later calls do nothing.
        /// </summary>
        /// <param name="reason">The reason the endpoint is being stopped.</param>
        /// <param name="tryBeGentle">A hint forwarded to BeginStop indicating whether the transport should
        /// attempt a graceful stop; the base class does not interpret it.</param>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Safe to call from any thread, including concurrently with
        /// other stop calls.</description></item>
        /// <item><description><b>Preconditions/Postconditions</b>: The endpoint stops accepting new work
        /// immediately, while work already accepted may still drain. Stopping an endpoint that was never
        /// started moves it directly to Stopped; stopping one that is still starting defers the transition
        /// until FinishStart completes. A stopped endpoint cannot be started again.</description></item>
        /// <item><description><b>Other notes</b>: The reason becomes the endpoint's stop reason. If the
        /// endpoint had started, the reason is supplied to the handler through OnStopped.</description></item>
        /// </list>
        /// </remarks>
        public void Stop(StopReason reason, bool tryBeGentle)
        {
            if (Interlocked.CompareExchange(ref _stopReason, reason, null) == null)
            {
                _actionQueue.Schedule(() =>
                {
                    switch (_currentStage)
                    {
                        case State.Constructed:
                            _currentStage = State.Stopped;
                            break;
                        case State.Starting:
                            // wait for FinishStart
                            break;
                        case State.Started:
                            _currentStage = State.Stopping;
                            BeginStop(reason, tryBeGentle);
                            break;
                        case State.Stopping:
                        case State.Stopped:
                            throw new InvalidOperationException($"Impossible RawEndpoint.Stage '{_currentStage}'");
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                });
            }
        }

        /// <summary>
        /// Performs the transport-specific stop work. Invoked on the serial action queue after the endpoint
        /// enters the stopping stage.
        /// </summary>
        /// <param name="reason">The reason the endpoint is being stopped.</param>
        /// <param name="tryBeGentle">A hint indicating whether a graceful stop should be attempted.</param>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Called on the serial action queue; never concurrently with
        /// another transition.</description></item>
        /// <item><description><b>Preconditions/Postconditions</b>: The implementation must call FinishStop
        /// exactly once, either synchronously or later from any thread. An exception fails the endpoint
        /// hard.</description></item>
        /// </list>
        /// </remarks>
        protected abstract void BeginStop(StopReason reason, bool tryBeGentle);

        /// <summary>
        /// Completes the stop transition, setting the stage to Stopped and invoking the handler's stop
        /// notification.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: The transition is serialized on the internal action queue;
        /// the call may be scheduled from any thread.</description></item>
        /// <item><description><b>Preconditions/Postconditions</b>: Intended to be called exactly once per
        /// endpoint by BeginStop or its continuation, while the endpoint is in the stopping stage. An invalid
        /// stage fails the endpoint hard.</description></item>
        /// </list>
        /// </remarks>
        protected void FinishStop()
        {
            _actionQueue.Schedule(() =>
            {
                if (_currentStage != State.Stopping)
                {
                    throw new InvalidOperationException($"Stage expected to be 'Stopping' but was {_currentStage}");
                }

                _currentStage = State.Stopped;
                _handler.OnStopped(_stopReason!);
            });
        }

        #endregion

        /// <summary>
        /// Transmits a single outbound buffer through the concrete carrier.
        /// </summary>
        /// <param name="bufferToSend">The buffer to transmit. Ownership transfers to the implementation.</param>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Invoked only on the serial action queue, never concurrently
        /// with another invocation.</description></item>
        /// <item><description><b>Preconditions/Postconditions</b>: The implementation receives ownership of
        /// the buffer and must release it exactly once, including when the carrier cannot accept it. It must
        /// not throw.</description></item>
        /// <item><description><b>Side effects</b>: If the override throws, the endpoint fails hard and the
        /// buffer is not released by the base class.</description></item>
        /// </list>
        /// </remarks>
        protected abstract void DoSendRaw(UnionDataList bufferToSend);

        /// <summary>
        /// Populates dst with the IControl interfaces exposed by this endpoint, optionally filtered by
        /// predicate.
        /// </summary>
        /// <param name="dst">The list that receives the exposed controls.</param>
        /// <param name="predicate">An optional filter; when provided, a control is added only if it is
        /// accepted.</param>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Other notes</b>: The base implementation adds no controls; overrides add the
        /// endpoint-specific controls.</description></item>
        /// </list>
        /// </remarks>
        public virtual void GetControls(List<IControl> dst, Predicate<IControl>? predicate = null)
        {
        }
    }
}
