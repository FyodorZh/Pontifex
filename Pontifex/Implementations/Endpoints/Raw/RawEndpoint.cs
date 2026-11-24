using System;
using System.Collections.Generic;
using System.Threading;
using Actuarius.Memory;
using Pontifex.StopReasons;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Raw
{
    /// <inheritdoc cref="IRawEndpointImp"/>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Other notes</b>: State transitions and handler dispatch are serialized by an
    /// internal action queue. An exception that escapes a queued transition fails the endpoint hard: it is
    /// logged, the stop reason is set to an ExceptionFail, and FailHard is invoked. A throwing
    /// <c>OnReceived</c> is not such a transition failure: it is logged and isolated, and the endpoint keeps
    /// running. The base implementation of FailHard delivers the handler's stop notification at most once
    /// when a start was attempted, then transitions the stage to Stopped; derived classes override it to
    /// release resources.</description></item>
    /// <item><description><b>Other notes</b>: <see cref="RaiseStopped"/> invokes each subscriber of the public
    /// <see cref="Stopped"/> event inside its own exception boundary, so one throwing subscriber cannot skip
    /// the others.</description></item>
    /// </list>
    /// </remarks>
    public abstract class RawEndpoint : IRawEndpointImp
    {
        private readonly long _bufferCapacityBytes;
        private readonly IRawHandler _handler;

        /// <summary>
        /// The serial action queue that executes all endpoint state transitions and handler dispatch, one
        /// action at a time.
        /// </summary>
        protected readonly ISerialActionQueue _actionQueue;

        private long _totalBuffersSize;
        private int _pendingSendMessages;

        private volatile int _messageMaxByteSize;
        private int _messageMaxByteSizeSet;

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

        private int _stoppedRaised;

        private int _handlerStopped;

        private readonly Action<SendWorkItem> _sendWorkItemAction;
        private readonly Action<UnionDataList> _receiveAction;

        /// <inheritdoc/>
        public IEndPoint RemoteEndPoint { get; }

        /// <inheritdoc/>
        public int MessageMaxByteSize => _messageMaxByteSize;

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

        /// <inheritdoc/>
        public bool IsRunning => _currentStage == State.Started && _stopReason == null;

        /// <summary>
        /// Gets a value indicating whether a stop has already been requested for this endpoint.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Safe to read concurrently; the observed value is a
        /// best-effort snapshot.</description></item>
        /// <item><description><b>Other notes</b>: The flag latches synchronously in <see cref="Stop"/>,
        /// before the serialized stop transition runs. A derived class uses it to suppress work that was
        /// scheduled before the stop was requested.</description></item>
        /// </list>
        /// </remarks>
        protected bool IsStopRequested => _stopReason != null;

        /// <inheritdoc/>
        public event Action<StopReason>? Stopped;

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
            _messageMaxByteSize = messageMaxByteSize;

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

            _sendWorkItemAction = workItem =>
            {
                UnionDataList msg = workItem.Message;
                SendSlotReservation reservation = workItem.Reservation;
                int size = msg.GetDataSize();
                try
                {
                    if (_currentStage == State.Started)
                    {
                        DoSendRaw(msg, reservation);
                    }
                    else
                    {
                        msg.Release();
                        reservation.ReleaseOnce();
                    }
                }
                finally
                {
                    Interlocked.Add(ref _totalBuffersSize, -size);
                    Interlocked.Add(ref _pendingSendMessages, -1);
                    try
                    {
                        OnPendingSendChanged(-size, -1);
                    }
                    catch (Exception ex)
                    {
                        Log.wtf(ex);
                    }
                }
            };

            _receiveAction = msg =>
            {
                if (IsRunning)
                {
                    try
                    {
                        _handler.OnReceived(msg);
                    }
                    catch (Exception ex)
                    {
                        // Handler failures are isolated: the delivery was consumed by the handler
                        // call, so only that delivery stops. The endpoint keeps running and the
                        // message ownership stays with the handler. A handler that throws without
                        // releasing its reference violates its contract; the endpoint must not
                        // release the reference on its behalf.
                        Log.wtf(ex);
                    }
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
        /// implementation delivers the handler's stop notification - at most once - when the endpoint had a
        /// start attempt in progress or had started, and then transitions the stage to Stopped; derived
        /// classes override to release unmanaged resources and terminate their work.
        /// </summary>
        /// <param name="reason">The error description that caused the failure.</param>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Invoked on the thread that executed the failing action;
        /// the base implementation delivers the stop notification and sets the stage to
        /// Stopped.</description></item>
        /// <item><description><b>Preconditions/Postconditions</b>: The base implementation notifies the
        /// handler at most once when the stage is Starting, Started, or Stopping. A Constructed endpoint has
        /// no start attempt and a Stopped endpoint has already notified, so neither notifies the handler.
        /// The stage is then set to Stopped and the public <see cref="Stopped"/> event is raised; the
        /// override must not throw.</description></item>
        /// <item><description><b>Other notes</b>: This method guarantees at most one notification per
        /// endpoint, not that the handler observes one. An unreliable subclass may gate the notification
        /// through its handler gate, so a start-attempted endpoint can legitimately notify no
        /// handler.</description></item>
        /// </list>
        /// </remarks>
        protected virtual void FailHard(StopReason reason)
        {
            try
            {
                if (_currentStage == State.Starting ||
                    _currentStage == State.Started ||
                    _currentStage == State.Stopping)
                {
                    NotifyHandlerStoppedOnce(reason);
                }
            }
            finally
            {
                _currentStage = State.Stopped;
                RaiseStopped(reason);
            }
        }

        /// <summary>
        /// Delivers the handler's stop notification at most once per endpoint, isolating handler failures.
        /// </summary>
        /// <param name="reason">The reason passed to the handler.</param>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Safe to call from any thread; the one-shot latch is
        /// atomic.</description></item>
        /// <item><description><b>Other notes</b>: The terminal stop paths compete for one latch, so a path
        /// that follows an already completed stop is silent even when it produces a different reason. A
        /// handler exception is logged and swallowed; it does not prevent the terminal
        /// transition.</description></item>
        /// </list>
        /// </remarks>
        private void NotifyHandlerStoppedOnce(StopReason reason)
        {
            if (Interlocked.Exchange(ref _handlerStopped, 1) != 0)
            {
                return;
            }

            try
            {
                _handler.OnStopped(reason);
            }
            catch (Exception ex)
            {
                Log.wtf(ex);
            }
        }

        /// <summary>
        /// Raises <see cref="Stopped"/> at most once, isolating the failure of each subscriber.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Safe to call from any thread; the one-shot latch is
        /// atomic.</description></item>
        /// <item><description><b>Other notes</b>: Every subscriber of the captured invocation list is invoked
        /// inside its own try/catch, so a throwing subscriber is logged and does not prevent later
        /// subscribers from running.</description></item>
        /// </list>
        /// </remarks>
        private void RaiseStopped(StopReason reason)
        {
            if (Interlocked.Exchange(ref _stoppedRaised, 1) != 0)
            {
                return;
            }

            Action<StopReason>? subscribers = Stopped;
            if (subscribers == null)
            {
                return;
            }

            foreach (Action<StopReason> subscriber in subscribers.GetInvocationList())
            {
                try
                {
                    subscriber(reason);
                }
                catch (Exception ex)
                {
                    Log.wtf(ex);
                }
            }
        }

        #region Start

        /// <inheritdoc/>
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
        /// <item><description><b>Other notes</b>: Endpoint state is checked first, then message validity,
        /// then the message size, then capacity. A successful admission requests processing of the internal
        /// action queue.</description></item>
        /// </list>
        /// </remarks>
        protected SendResult ScheduleToSend(UnionDataList bufferToSend)
        {
            if (!CanSendNow(out var errorCode))
            {
                // State is checked before the buffer, so a stopped endpoint reports the state result
                // even for a null buffer. A non-null buffer still belongs to the endpoint on every
                // non-InvalidMessage result, so it is released here; a dead buffer stays with the
                // caller, as the caller owns it on InvalidMessage.
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

            int bufferSize = bufferToSend.GetDataSize();
            if (bufferSize > MessageMaxByteSize)
            {
                bufferToSend.Release();
                return SendResult.MessageTooBig;
            }

            SendSlotReservation? reservation = TryReserveSendSlot();
            if (reservation == null)
            {
                bufferToSend.Release();
                return SendResult.BufferOverflow;
            }

            Interlocked.Add(ref _totalBuffersSize, bufferSize);
            Interlocked.Add(ref _pendingSendMessages, 1);
            try
            {
                OnPendingSendChanged(bufferSize, 1);
            }
            catch (Exception ex)
            {
                Log.wtf(ex);
            }

            if (_totalBuffersSize > _bufferCapacityBytes)
            {
                Interlocked.Add(ref _totalBuffersSize, -bufferSize);
                Interlocked.Add(ref _pendingSendMessages, -1);
                try
                {
                    OnPendingSendChanged(-bufferSize, -1);
                }
                catch (Exception ex)
                {
                    Log.wtf(ex);
                }
                bufferToSend.Release();
                reservation.ReleaseOnce();
                return SendResult.BufferOverflow;
            }

            var workItem = new SendWorkItem(bufferToSend, reservation);
            if (!_actionQueue.Schedule(workItem, _sendWorkItemAction, item =>
                {
                    int droppedSize = item.Message.GetDataSize();
                    Interlocked.Add(ref _totalBuffersSize, -droppedSize);
                    Interlocked.Add(ref _pendingSendMessages, -1);
                    try
                    {
                        OnPendingSendChanged(-droppedSize, -1);
                    }
                    catch (Exception ex)
                    {
                        Log.wtf(ex);
                    }
                    item.Message.Release();
                    item.Reservation.ReleaseOnce();
                }))
            {
                Interlocked.Add(ref _totalBuffersSize, -bufferSize);
                Interlocked.Add(ref _pendingSendMessages, -1);
                try
                {
                    OnPendingSendChanged(-bufferSize, -1);
                }
                catch (Exception ex)
                {
                    Log.wtf(ex);
                }
                bufferToSend.Release();
                reservation.ReleaseOnce();
                return SendResult.Error;
            }

            return SendResult.Ok;
        }

        /// <summary>
        /// Applies the negotiated maximum message size exactly once.
        /// </summary>
        /// <param name="value">The negotiated inclusive maximum message size; must be at least 1.</param>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Safe to call from any thread; the one-shot latch is
        /// atomic.</description></item>
        /// <item><description><b>Preconditions/Postconditions</b>: The endpoint must be starting or started. A
        /// value below 1, a second call, or a call while the endpoint is stopping or stopped throws
        /// <see cref="InvalidOperationException"/>.</description></item>
        /// <item><description><b>Other notes</b>: A server endpoint that already knows the agreed value at
        /// construction time seals it with <see cref="MarkMessageMaxByteSizeSet"/> instead, because the base
        /// constructor stage is not yet starting.</description></item>
        /// </list>
        /// </remarks>
        protected void SetMessageMaxByteSizeOnce(int value)
        {
            if (value < 1)
            {
                throw new InvalidOperationException("The negotiated message maximum must be at least 1.");
            }

            if (_currentStage != State.Starting && _currentStage != State.Started)
            {
                throw new InvalidOperationException(
                    $"Cannot set the negotiated message maximum while the endpoint is {_currentStage}.");
            }

            if (Interlocked.Exchange(ref _messageMaxByteSizeSet, 1) != 0)
            {
                throw new InvalidOperationException("The negotiated message maximum is already set.");
            }

            _messageMaxByteSize = value;
        }

        /// <summary>
        /// Seals the message maximum that was supplied to the constructor so that
        /// <see cref="SetMessageMaxByteSizeOnce"/> cannot change it afterwards.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Safe to call from any thread; the latch is
        /// atomic.</description></item>
        /// <item><description><b>Other notes</b>: Used by the server endpoint, whose negotiated value is
        /// already known when the endpoint is constructed.</description></item>
        /// </list>
        /// </remarks>
        protected void MarkMessageMaxByteSizeSet()
        {
            Interlocked.Exchange(ref _messageMaxByteSizeSet, 1);
        }

        /// <inheritdoc/>
        public virtual SendResult Send(UnionDataList bufferToSend)
        {
            return ScheduleToSend(bufferToSend);
        }

        /// <summary>
        /// Reports the signed change of this endpoint's pending outbound reservation: +size/+1 when bytes
        /// are reserved for a buffer, -size/-1 when the reservation is released (transmitted, dropped by a
        /// stop, aborted by DoSendRaw throwing, or rolled back because admission failed). Emitted before
        /// the buffer enters the serial queue, so a rejected admission appears as a matching +/− pair.
        /// May be invoked from different threads; overrides must aggregate atomically and must not throw.
        /// </summary>
        /// <param name="deltaBytes">The signed change of the reserved payload bytes.</param>
        /// <param name="deltaMessages">The signed change of the reserved message count.</param>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Invoked from any thread that reserves or releases a buffer,
        /// including the serial action queue; an override must be safe for concurrent use.</description></item>
        /// <item><description><b>Other notes</b>: The base implementation does nothing. The reported deltas are
        /// symmetric on every path, so their sum equals the endpoint's current pending reservation. An exception
        /// thrown by an override is logged and does not affect buffer accounting.</description></item>
        /// </list>
        /// </remarks>
        protected virtual void OnPendingSendChanged(int deltaBytes, int deltaMessages) { }

        #endregion

        #region Stop

        /// <inheritdoc/>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Other notes</b>: Only the first caller wins; later calls are ignored. The
        /// handler stop notification is delivered at most once for a start-attempted endpoint, and a stop
        /// before the first start does not notify the handler at all (the public
        /// <see cref="Stopped"/> event is still raised). An unreliable subclass may gate the notification
        /// through its handler gate.</description></item>
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
                            RaiseStopped(reason);
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
                            // Teardown has already been started: FinishStart promoted a stop latched while
                            // starting, or the endpoint failed hard. The CAS above guarantees at most one
                            // queued stop action, so there is nothing left to do here.
                            break;
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
        /// Completes the stop transition, setting the stage to Stopped and delivering the handler's stop
        /// notification at most once.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: The transition is serialized on the internal action queue;
        /// the call may be scheduled from any thread.</description></item>
        /// <item><description><b>Preconditions/Postconditions</b>: Intended to be called exactly once per
        /// endpoint by BeginStop or its continuation, while the endpoint is in the stopping stage. A call that
        /// arrives after the endpoint already reached Stopped, for example because a hard failure completed
        /// the endpoint, is a no-op. Any other invalid stage fails the endpoint hard.</description></item>
        /// <item><description><b>Other notes</b>: The handler notification runs through the same one-shot
        /// latch as <see cref="FailHard"/>, so a hard failure followed by a stop completion notifies the
        /// handler only once, with the first reason. An unreliable subclass may gate the notification through
        /// its handler gate.</description></item>
        /// </list>
        /// </remarks>
        protected void FinishStop()
        {
            _actionQueue.Schedule(() =>
            {
                if (_currentStage == State.Stopped)
                {
                    return;
                }

                if (_currentStage != State.Stopping)
                {
                    throw new InvalidOperationException($"Stage expected to be 'Stopping' but was {_currentStage}");
                }

                _currentStage = State.Stopped;
                try
                {
                    NotifyHandlerStoppedOnce(_stopReason!);
                }
                finally
                {
                    RaiseStopped(_stopReason!);
                }
            });
        }

        #endregion

        /// <summary>
        /// Reserves one outbound admission slot for a message that passed the state, validity, and size
        /// checks.
        /// </summary>
        /// <returns>A reservation token that must be released exactly once, or <see langword="null"/> when the
        /// transport-specific admission budget is exhausted.</returns>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Safe for concurrent calls from any thread; an override must
        /// be safe for concurrent use.</description></item>
        /// <item><description><b>Other notes</b>: The base implementation returns a shared no-op token, so the
        /// default path reserves nothing. An override that bounds the number of accepted messages returns a
        /// token whose <see cref="SendSlotReservation.ReleaseOnce"/> returns the slot. A null return is reported
        /// as <see cref="SendResult.BufferOverflow"/> and the buffer is released by the caller.</description></item>
        /// </list>
        /// </remarks>
        protected virtual SendSlotReservation? TryReserveSendSlot()
        {
            return SendSlotReservation.NoOpReservation;
        }

        /// <summary>
        /// Creates a send-slot reservation that invokes the given release action once.
        /// </summary>
        /// <param name="onRelease">The action that returns the reserved slot; must not be
        /// <see langword="null"/>.</param>
        /// <returns>The reservation token.</returns>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Other notes</b>: The factory keeps the token constructor out of the carrier
        /// surface while derived transports in other assemblies can still create
        /// reservations.</description></item>
        /// </list>
        /// </remarks>
        protected static SendSlotReservation CreateSendSlotReservation(Action onRelease)
        {
            return new SendSlotReservation(onRelease);
        }

        /// <summary>
        /// Transmits one outbound buffer together with its send-slot reservation.
        /// </summary>
        /// <param name="bufferToSend">The buffer to transmit. Ownership transfers to the implementation.</param>
        /// <param name="reservation">The reservation returned by <see cref="TryReserveSendSlot"/>. It must be
        /// released exactly once; the default implementation releases it after the abstract
        /// <see cref="DoSendRaw(UnionDataList)"/> returns or throws.</param>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Other notes</b>: Carriers that keep their existing abstract override need no
        /// change. A carrier that must hand the buffer and the reservation to a worker overrides this overload;
        /// the worker then owns the token and releases it when it commits or discards the
        /// message.</description></item>
        /// </list>
        /// </remarks>
        protected virtual void DoSendRaw(UnionDataList bufferToSend, SendSlotReservation reservation)
        {
            try
            {
                DoSendRaw(bufferToSend);
            }
            finally
            {
                reservation.ReleaseOnce();
            }
        }

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
        /// One accepted outbound message and its send-slot reservation.
        /// </summary>
        private sealed class SendWorkItem
        {
            public SendWorkItem(UnionDataList message, SendSlotReservation reservation)
            {
                Message = message;
                Reservation = reservation;
            }

            public UnionDataList Message { get; }

            public SendSlotReservation Reservation { get; }
        }

        /// <summary>
        /// One outbound admission slot.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Other notes</b>: The token is released exactly once; a second release is a
        /// no-op. The base path uses a shared no-op instance because it reserves nothing.</description></item>
        /// </list>
        /// </remarks>
        protected class SendSlotReservation
        {
            private static readonly SendSlotReservation NoOpInstance = new SendSlotReservation(NoOpRelease);

            private readonly Action _onRelease;
            private int _released;

            /// <summary>
            /// Initializes a new reservation that invokes the given release action once.
            /// </summary>
            /// <param name="onRelease">The action that returns the reserved slot; must not be
            /// <see langword="null"/>.</param>
            internal SendSlotReservation(Action onRelease)
            {
                _onRelease = onRelease ?? throw new ArgumentNullException(nameof(onRelease));
            }

            /// <summary>
            /// Gets the shared no-op reservation.
            /// </summary>
            internal static SendSlotReservation NoOpReservation => NoOpInstance;

            /// <summary>
            /// Releases the reservation exactly once.
            /// </summary>
            public void ReleaseOnce()
            {
                if (Interlocked.Exchange(ref _released, 1) == 0)
                {
                    _onRelease();
                }
            }

            private static void NoOpRelease()
            {
            }
        }

        /// <inheritdoc/>
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
