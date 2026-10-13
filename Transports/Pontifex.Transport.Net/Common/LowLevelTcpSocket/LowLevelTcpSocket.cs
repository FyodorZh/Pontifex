using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Actuarius.Collections;
using Actuarius.Memory;

namespace Pontifex.NetSockets.Tcp
{
    /// <remarks>
    /// <para><b>SocketAsyncEventArgs (A2):</b> each instance owns two <see cref="SocketAsyncEventArgs"/>
    /// (send + receive) that are created on construction and disposed on teardown; they are not pooled
    /// across connections. Under high connection churn consider an external pool seam.</para>
    /// <para><b>Teardown outside gates (C05/P06):</b> buffer releases, event-args disposal, and the
    /// directional socket shutdown are external calls and never run under a directional I/O gate.
    /// Ownership is reserved and the resources are detached under a short critical section; the
    /// external work runs detached; completion/errors are recorded afterwards. Abort state admission
    /// therefore never waits for a blocked pool return or accessor call, while normal completion still
    /// waits for the reserved cleanup attempts. A directional shutdown reserves its ending state
    /// before the external call so no new operation starts while it is in flight, and a concurrent
    /// abort keeps first-terminal precedence.</para>
    /// <para><b>Bounded teardown publication (C05):</b> the physical teardown (both-direction
    /// shutdown, accessor disposal, aggregate finalization) is reserved exactly once per connection
    /// and executed either synchronously on a best-effort caller (<see cref="Dispose"/> and
    /// directional faults) or through the internal <see cref="ITeardownExecutor"/> for
    /// <see cref="DisposeAsync(TimeSpan)"/>, <see cref="DisposeAsync()"/>, and deadline escalation.
    /// The finite overload arms its budget after validation and returns to its caller before any
    /// blocking native work; if the budget elapses it force-publishes the aggregate and both
    /// directional results while cleanup may still be pending. A disposal failure completing after
    /// that immutable publication is reported through the diagnostic channel exactly once.</para>
    /// <para><b>Byte pressure (A6):</b> the transport does not bound queued or retained bytes. The
    /// producer must bound the outbound source and consumers must bound how long they retain receive
    /// buffers; the transport only accounts for its single in-flight operation per direction.</para>
    /// <para><b>Idle / stalled-I/O (A18):</b> there is no idle or read/write deadline for normal
    /// operation. Enforce liveness externally (application keepalive) and bound teardown with
    /// <see cref="DisposeAsync(TimeSpan)"/> or <see cref="ITcpSocketSender.Shutdown(bool, TimeSpan)"/>.</para>
    /// </remarks>
    internal class LowLevelTcpSocket : ILowLevelTcpSocket
    {
        private const int DefaultReceiveBufferSize = 16 * 1024;

        /// <summary>
        /// Largest finite deadline accepted by <see cref="ValidateDeadline"/>: <c>int.MaxValue</c>
        /// milliseconds. Matches the range supported by the timer and delay targets in use.
        /// </summary>
        internal static readonly TimeSpan MaxFiniteDeadline = TimeSpan.FromMilliseconds(int.MaxValue);

        /// <summary>
        /// Validates a deadline accepted by the constructor, the sender shutdown overload, and bounded
        /// disposal. The accepted range is exactly <see cref="Timeout.InfiniteTimeSpan"/>, or
        /// <see cref="TimeSpan.Zero"/> through <see cref="int.MaxValue"/> milliseconds inclusive,
        /// compared by ticks. Rejecting an invalid value must happen before any resource creation or
        /// lifecycle mutation.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="deadline"/> is a finite negative value other than
        /// <see cref="Timeout.InfiniteTimeSpan"/>, or exceeds <see cref="MaxFiniteDeadline"/>.
        /// </exception>
        internal static void ValidateDeadline(TimeSpan deadline, string paramName)
        {
            long ticks = deadline.Ticks;
            if (ticks == Timeout.InfiniteTimeSpan.Ticks)
            {
                return;
            }

            if (ticks < 0 || ticks > MaxFiniteDeadline.Ticks)
            {
                throw new ArgumentOutOfRangeException(
                    paramName,
                    deadline,
                    $"The deadline must be Timeout.InfiniteTimeSpan or between TimeSpan.Zero and " +
                    $"{MaxFiniteDeadline} inclusive (int.MaxValue milliseconds).");
            }
        }

        private readonly ISocketAccessor _socket;
        private readonly SenderImpl _sender;
        private readonly ReceiverImpl _receiver;
        private readonly RemoteHalfClosePolicy _remoteHalfClosePolicy;
        private readonly Action<Exception>? _onCallbackError;
        private readonly TimeSpan? _gracefulShutdownDeadline;
        private readonly TaskCompletionSource<SocketCompletionResult> _completionSource =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly object _lifecycleGate = new();
        private readonly ITeardownExecutor _teardownExecutor;
        private readonly IDeadlineScheduler _disposalScheduler;

        // Shared per-connection bounded-disposal deadline (stage 19): earliest expiration wins and
        // one signal serves every concurrent waiter. The deadline never becomes earlier than a
        // caller's own bound by reuse: a shorter waiter may shorten it, a longer waiter never
        // extends it. Guarded by _disposalDeadlineSync.
        private readonly object _disposalDeadlineSync = new();
        private int _disposalDeadlineGeneration;
        private bool _disposalDeadlineArmed;
        private bool _disposalDeadlineExpired;
        private long _disposalDeadlineStartTimestamp;
        private TimeSpan _disposalDeadlineDuration;
        private TaskCompletionSource<bool>? _disposalExpirySource;

        // Created once per connection: the executor queues this stable delegate instead of the
        // connection allocating a closure/task per teardown request.
        private readonly Action _teardownWork;

        private SenderCompletionResult? _sendResult;
        private ReceiverCompletionResult? _receiveResult;
        private SocketCompletionReason? _primaryReason;
        private Exception? _primaryError;
        private Exception? _secondaryError;
        private bool _abortRequested;

        // Physical teardown (socket shutdown + accessor disposal + aggregate finalization) is
        // reserved exactly once. Requested/queued/started/finished are tracked separately so a
        // deadline callback can publish logical results without waiting for any of them:
        // _abortRequested = requested (logical admission), _teardownReserved = queued/scheduled,
        // _teardownStarted/_teardownFinished = execution progress.
        private bool _teardownReserved;
        private bool _teardownStarted;
        private bool _teardownFinished;
        private bool _socketDisposeReserved;
        private bool _socketDisposeAttempted;
        private Exception? _socketDisposeError;
        private bool _socketDisposeDiagnosticReported;
        private bool _completionRecorded;
        private bool _disposed;
        private bool _sendPublished;
        private bool _receivePublished;

        public ITcpSocketSender Sender => _sender;
        public ITcpSocketReceiver Receiver => _receiver;
        public Task<SocketCompletionResult> Completion => _completionSource.Task;

        /// <param name="socket">
        /// The connected stream (TCP) socket. Ownership transfers to this instance, which shuts it down
        /// and disposes it during teardown.
        /// </param>
        /// <param name="dataToSendSource">
        /// Single-reader/single-writer outbound source. The transport is the sole reader; the producer is
        /// the sole writer. Queued-but-unsent buffers remain the producer's and must be drained by it
        /// after <see cref="ITcpSocketSender.SourceQuiesced"/> resolves (logical
        /// <see cref="ITcpSocketSender.Completion"/> may resolve earlier).
        /// </param>
        /// <param name="onReceived">
        /// Invoked inline on the socket completion thread and takes ownership of the delivered buffer
        /// (released by the consumer, even on throw). It must be short and non-blocking: it delays
        /// re-arming the next receive and quiesced <see cref="Completion"/> until it returns.
        /// </param>
        /// <param name="byteArraysPool">Pool that supplies receive buffers.</param>
        /// <param name="remoteHalfClosePolicy">Action to take when the remote half-closes (sends EOF).</param>
        /// <param name="onCallbackError">
        /// Diagnostic sink for callback failures and for cleanup/disposal failures that arrive after
        /// an immutable result has already been forced; never allowed to disrupt I/O or teardown.
        /// </param>
        /// <param name="receiveBufferSize">Capacity of every pooled receive buffer. Must be positive.</param>
        /// <param name="gracefulShutdownDeadline">
        /// Default deadline for a graceful send shutdown: applied to the first parameterless
        /// <see cref="ITcpSocketSender.Shutdown(bool)"/> graceful request and to the remote half-close
        /// <see cref="RemoteHalfClosePolicy.GracefulSendShutdown"/> policy. The deadline covers the
        /// entire shutdown/drain: expiry survives source-read and completion gaps, prevents any
        /// further send posting, and force-publishes unfinished logical termination. Explicit sender
        /// overloads override it (<see cref="Timeout.InfiniteTimeSpan"/> cancels an active deadline);
        /// repeated parameterless requests do not extend it. <see langword="null"/> waits
        /// indefinitely. Valid values are exactly <see cref="Timeout.InfiniteTimeSpan"/> or
        /// <see cref="TimeSpan.Zero"/> through <see cref="int.MaxValue"/> milliseconds inclusive
        /// (<see cref="MaxFiniteDeadline"/>); other values throw <see cref="ArgumentOutOfRangeException"/>
        /// before any event-args are created or ownership is transferred.
        /// </param>
        public LowLevelTcpSocket(
            ISocketAccessor socket,
            ISingleReaderWriterConcurrentQueue<IMultiRefByteArray> dataToSendSource,
            Action<IMultiRefByteArray> onReceived,
            IConcurrentPool<IMultiRefByteArray, int> byteArraysPool,
            RemoteHalfClosePolicy remoteHalfClosePolicy = RemoteHalfClosePolicy.KeepSendOpen,
            Action<Exception>? onCallbackError = null,
            int receiveBufferSize = DefaultReceiveBufferSize,
            TimeSpan? gracefulShutdownDeadline = null)
            : this(
                socket,
                dataToSendSource,
                onReceived,
                byteArraysPool,
                remoteHalfClosePolicy,
                onCallbackError,
                receiveBufferSize,
                gracefulShutdownDeadline,
                null)
        {
        }

        /// <summary>
        /// Internal constructor that additionally accepts the deadline scheduler seam used by
        /// deterministic stage tests and the physical-teardown executor seam. Production callers use
        /// the public overload.
        /// </summary>
        internal LowLevelTcpSocket(
            ISocketAccessor socket,
            ISingleReaderWriterConcurrentQueue<IMultiRefByteArray> dataToSendSource,
            Action<IMultiRefByteArray> onReceived,
            IConcurrentPool<IMultiRefByteArray, int> byteArraysPool,
            RemoteHalfClosePolicy remoteHalfClosePolicy,
            Action<Exception>? onCallbackError,
            int receiveBufferSize,
            TimeSpan? gracefulShutdownDeadline,
            IDeadlineScheduler? deadlineScheduler,
            ITeardownExecutor? teardownExecutor = null,
            IDeadlineScheduler? disposalScheduler = null)
        {
            _socket = socket ?? throw new ArgumentNullException(nameof(socket));
            if (dataToSendSource == null) throw new ArgumentNullException(nameof(dataToSendSource));
            if (onReceived == null) throw new ArgumentNullException(nameof(onReceived));
            if (byteArraysPool == null) throw new ArgumentNullException(nameof(byteArraysPool));
            if (!Enum.IsDefined(typeof(RemoteHalfClosePolicy), remoteHalfClosePolicy))
            {
                throw new ArgumentOutOfRangeException(nameof(remoteHalfClosePolicy));
            }
            if (receiveBufferSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(receiveBufferSize));
            }

            if (gracefulShutdownDeadline.HasValue)
            {
                ValidateDeadline(gracefulShutdownDeadline.Value, nameof(gracefulShutdownDeadline));
            }

            _remoteHalfClosePolicy = remoteHalfClosePolicy;
            _onCallbackError = onCallbackError;
            _gracefulShutdownDeadline = gracefulShutdownDeadline;
            _teardownExecutor = teardownExecutor ?? ThreadPoolTeardownExecutor.Instance;
            _disposalScheduler = disposalScheduler ?? new SystemDeadlineScheduler();
            _teardownWork = RunTeardown;
            // Successful construction is the ownership-transfer boundary. If the receiver cannot be
            // constructed, the already-created sender args are rolled back without masking the
            // initiating exception; the supplied accessor/source/pool remain caller-owned.
            _sender = new SenderImpl(
                this, socket, dataToSendSource, deadlineScheduler ?? new SystemDeadlineScheduler());
            try
            {
                _receiver = new ReceiverImpl(this, socket, onReceived, byteArraysPool, receiveBufferSize);
            }
            catch
            {
                _sender.RollbackConstruction();
                throw;
            }
        }

        public void Close()
        {
            Dispose();
        }

        public void Dispose()
        {
            MarkDisposed();
            RequestAbort(null);
        }

        public ValueTask DisposeAsync()
        {
            MarkDisposed();
            BeginQueuedAbort(SocketCompletionReason.Disposed, null);
            return Completion.IsCompleted ? default : WaitForCompletionAsync();
        }

        /// <summary>
        /// Requests a logical abort, queues the connection's single reserved physical teardown to the
        /// internal executor, and waits for completion, force-finalizing the logical result if
        /// teardown does not finish within <paramref name="timeout"/>.
        /// </summary>
        /// <remarks>
        /// <para>The budget starts at call entry after validation and non-blocking admission; this
        /// method never synchronously performs blocking physical teardown before its first
        /// asynchronous return. An already-completed connection returns immediately without
        /// scheduling timer or task state (P04). Normal completion still awaits physical cleanup and
        /// receive callbacks; deadline-forced completion resolves the aggregate and both directional
        /// tasks while actual cleanup and <see cref="ITcpSocketSender.SourceQuiesced"/> may remain
        /// pending.</para>
        /// <para><paramref name="timeout"/> must be exactly <see cref="Timeout.InfiniteTimeSpan"/>
        /// or in <c>[TimeSpan.Zero, int.MaxValue milliseconds]</c>; an invalid value throws
        /// <see cref="ArgumentOutOfRangeException"/> before any disposal side effect.</para>
        /// <para>Concurrent bounded waiters share one per-connection disposal deadline with
        /// earliest-expiration semantics: a shorter waiter may shorten the shared deadline, a longer
        /// waiter never extends an earlier caller's bound, and the shared timer is invalidated once
        /// it fires or completion resolves.</para>
        /// </remarks>
        public ValueTask DisposeAsync(TimeSpan timeout)
        {
            ValidateDeadline(timeout, nameof(timeout));

            MarkDisposed();
            BeginQueuedAbort(SocketCompletionReason.Disposed, null);

            if (Completion.IsCompleted)
            {
                // Fast path (P04): a finished connection must not create a timer, a TCS, or a
                // task-race state for a bounded disposal.
                return default;
            }

            if (timeout == Timeout.InfiniteTimeSpan)
            {
                return WaitForCompletionAsync();
            }

            return DisposeBoundedAsync(timeout);
        }

        private async ValueTask WaitForCompletionAsync()
        {
            await Completion.ConfigureAwait(false);
        }

        private async ValueTask DisposeBoundedAsync(TimeSpan timeout)
        {
            Task expiry = ArmDisposalDeadline(timeout);
            Task completed = await Task.WhenAny(Completion, expiry).ConfigureAwait(false);
            if (completed != Completion)
            {
                ForceFinalize(new TimeoutException($"Socket shutdown did not complete within {timeout}."));
                return;
            }

            // Completion won: the shared timer loses and must not remain armed.
            CancelDisposalDeadline();
            await Completion.ConfigureAwait(false);
        }

        /// <summary>
        /// Arms or shortens the shared per-connection disposal deadline and returns the waiter
        /// signal. Earliest-expiration wins: a later/longer waiter reuses the established deadline
        /// and cannot extend another caller's bound. The shared signal is created once and completed
        /// when any armed generation fires.
        /// </summary>
        private Task ArmDisposalDeadline(TimeSpan timeout)
        {
            lock (_disposalDeadlineSync)
            {
                if (_disposalExpirySource == null)
                {
                    _disposalExpirySource = new TaskCompletionSource<bool>(
                        TaskCreationOptions.RunContinuationsAsynchronously);
                }

                if (_disposalDeadlineExpired)
                {
                    return _disposalExpirySource.Task;
                }

                if (_disposalDeadlineArmed)
                {
                    TimeSpan remaining = _disposalDeadlineDuration
                                         - _disposalScheduler.GetElapsedTime(_disposalDeadlineStartTimestamp);
                    if (timeout >= remaining)
                    {
                        // Reuse the established (earlier or equal) bound; never extend it.
                        return _disposalExpirySource.Task;
                    }
                }

                _disposalDeadlineGeneration++;
                int generation = _disposalDeadlineGeneration;
                _disposalDeadlineArmed = true;
                _disposalDeadlineStartTimestamp = _disposalScheduler.GetTimestamp();
                _disposalDeadlineDuration = timeout;
                _disposalScheduler.Schedule(timeout, () => OnDisposalDeadline(generation));
                return _disposalExpirySource.Task;
            }
        }

        private void OnDisposalDeadline(int generation)
        {
            TaskCompletionSource<bool>? source;
            lock (_disposalDeadlineSync)
            {
                if (!_disposalDeadlineArmed || generation != _disposalDeadlineGeneration)
                {
                    return; // obsolete generation or already-resolved deadline: ignore
                }

                _disposalDeadlineArmed = false;
                _disposalDeadlineExpired = true;
                source = _disposalExpirySource;
            }

            source?.TrySetResult(true);
        }

        /// <summary>
        /// Cancels the shared disposal timer because completion won. The shared signal is never
        /// completed by this path, so no waiter observes a spurious expiry; the generation bump
        /// invalidates a callback that was already dispatched by a real timer.
        /// </summary>
        private void CancelDisposalDeadline()
        {
            bool cancelScheduler;
            lock (_disposalDeadlineSync)
            {
                cancelScheduler = _disposalDeadlineArmed;
                _disposalDeadlineArmed = false;
                _disposalDeadlineGeneration++;
            }

            if (cancelScheduler)
            {
                _disposalScheduler.Cancel();
            }
        }

        internal void ForceAbortFinalize(Exception reason)
        {
            // Logical admission and physical teardown are separated: the deadline callback reserves
            // and queues the single teardown, then publishes the forced logical result immediately
            // without waiting for any native disposal or reserved cleanup.
            MarkDisposed();
            BeginQueuedAbort(SocketCompletionReason.Disposed, reason);
            ForceFinalize(reason);
        }

        private void MarkDisposed()
        {
            lock (_lifecycleGate)
            {
                _disposed = true;
            }
        }

        /// <summary>
        /// Throws <see cref="ObjectDisposedException"/> once the socket has been disposed, so misuse of
        /// the directional endpoints after teardown is detected instead of silently no-oping.
        /// </summary>
        internal void ThrowIfDisposed()
        {
            lock (_lifecycleGate)
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(nameof(LowLevelTcpSocket));
                }
            }
        }

        private void ForceFinalize(Exception reason)
        {
            // Escape hatch for a bounded shutdown: publish even if a native operation never completes.
            SocketCompletionResult result;
            lock (_lifecycleGate)
            {
                if (_completionRecorded)
                {
                    return;
                }

                RecordAdditionalError(reason);
                _primaryReason ??= SocketCompletionReason.Faulted;
                _completionRecorded = true;
                _sendPublished = true;
                _receivePublished = true;
                result = new SocketCompletionResult(
                    _primaryReason.Value,
                    _primaryError,
                    _secondaryError,
                    _sendResult,
                    _receiveResult);
            }

            // The forced result is immutable from here on. The native disposal attempt belongs to the
            // reserved teardown execution (queued before this publication or already in flight); a
            // deadline callback must not perform a potentially blocking native call. A disposal
            // failure that completes after this publication is routed to the diagnostic channel by
            // TryDisposeSocket, exactly once.
            _sender.ForcePublish(reason);
            _receiver.ForcePublish(reason);

            _completionSource.TrySetResult(result);

            ReportSocketDisposeDiagnosticIfNeeded();
        }

        private void RegisterSendTermination(SenderCompletionResult result)
        {
            if (ReserveSendTermination(result))
            {
                ExecuteAbort();
            }
        }

        /// <summary>
        /// Records the sender terminal result and reserves the initiating fault's aggregate
        /// precedence before any external cleanup runs. Returns <see langword="true"/> when an
        /// abort is required; the caller must execute it outside every directional gate (see
        /// <see cref="ExecuteAbort"/>) so no cross-direction lock is taken while one is held.
        /// </summary>
        private bool ReserveSendTermination(SenderCompletionResult result)
        {
            lock (_lifecycleGate)
            {
                if (_sendResult.HasValue)
                {
                    return false;
                }

                _sendResult = result;
                return RecordFaultAndRequestAbort(result.Error);
            }
        }

        private void RegisterReceiveTermination(ReceiverCompletionResult result)
        {
            if (ReserveReceiveTermination(result))
            {
                ExecuteAbort();
            }
        }

        /// <summary>
        /// Records the receiver terminal result and reserves the initiating fault's aggregate
        /// precedence before any external cleanup runs. Returns <see langword="true"/> when an
        /// abort is required; the caller must execute it outside every directional gate (see
        /// <see cref="ExecuteAbort"/>).
        /// </summary>
        private bool ReserveReceiveTermination(ReceiverCompletionResult result)
        {
            lock (_lifecycleGate)
            {
                if (_receiveResult.HasValue)
                {
                    return false;
                }

                _receiveResult = result;
                return RecordFaultAndRequestAbort(result.Error);
            }
        }

        private void MarkSendPublished()
        {
            lock (_lifecycleGate)
            {
                _sendPublished = true;
            }

            TryFinalizeCompletion();
        }

        private void MarkReceivePublished(ReceiverCompletionResult result)
        {
            lock (_lifecycleGate)
            {
                _receivePublished = true;
            }

            TryFinalizeCompletion();

            if (result.Reason != ReceiverCompletionReason.RemoteEof)
            {
                return;
            }

            // Skip the half-close policy action if teardown already started (abort/dispose won the race).
            lock (_lifecycleGate)
            {
                if (_completionRecorded || _abortRequested)
                {
                    return;
                }
            }

            switch (_remoteHalfClosePolicy)
            {
                case RemoteHalfClosePolicy.KeepSendOpen:
                    break;
                case RemoteHalfClosePolicy.GracefulSendShutdown:
                    _sender.ShutdownCore(true, Timeout.InfiniteTimeSpan, true);
                    break;
                case RemoteHalfClosePolicy.AbortConnection:
                    RequestAbort(SocketCompletionReason.RemoteAborted, null);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void RequestAbort(Exception? error)
        {
            // A null error is a local teardown request.
            RequestAbort(SocketCompletionReason.Disposed, error);
        }

        private void RequestAbort(SocketCompletionReason reason, Exception? error)
        {
            if (AdmitAbort(reason, error))
            {
                ExecuteAbort();
            }
        }

        /// <summary>
        /// Logical abort admission only: records the initiating outage precedence under the
        /// lifecycle gate without performing any external teardown. Returns <see langword="true"/>
        /// when this call established the abort request. Callers that must not block on physical
        /// teardown (async/bounded disposal, deadline escalation) follow it with
        /// <see cref="QueueTeardown"/>; synchronous best-effort callers use
        /// <see cref="RequestAbort(SocketCompletionReason, Exception?)"/>.
        /// </summary>
        private bool AdmitAbort(SocketCompletionReason reason, Exception? error)
        {
            lock (_lifecycleGate)
            {
                if (_completionRecorded)
                {
                    return false;
                }

                if (error != null)
                {
                    RecordAdditionalError(error);
                }

                if (_abortRequested)
                {
                    return false;
                }

                _abortRequested = true;
                _primaryReason ??= error == null
                    ? reason
                    : SocketCompletionReason.Faulted;
                if (_primaryReason == SocketCompletionReason.Faulted)
                {
                    _primaryError ??= error;
                }

                return true;
            }
        }

        /// <summary>
        /// Logical admission plus non-blocking physical-teardown queueing. Safe to call after an
        /// already-admitted request (the reservation makes teardown exactly-once).
        /// </summary>
        private void BeginQueuedAbort(SocketCompletionReason reason, Exception? error)
        {
            AdmitAbort(reason, error);
            QueueTeardown();
        }

        private bool RecordFaultAndRequestAbort(Exception? error)
        {
            if (error == null)
            {
                return false;
            }

            RecordAdditionalError(error);
            if (_abortRequested)
            {
                return false;
            }

            _abortRequested = true;
            return true;
        }

        private void RecordAdditionalError(Exception error)
        {
            if (_primaryReason == null)
            {
                _primaryReason = SocketCompletionReason.Faulted;
                _primaryError = error;
            }
            else if (_primaryError != null && AreEquivalent(_primaryError, error))
            {
                // A value-equal duplicate of the primary failure must not consume the single
                // secondary-error slot.
            }
            else if (_secondaryError != null && AreEquivalent(_secondaryError, error))
            {
                // Already captured; do not let a duplicate displace the report.
            }
            else
            {
                _secondaryError ??= error;
            }
        }

        private static bool AreEquivalent(Exception first, Exception second)
        {
            if (ReferenceEquals(first, second))
            {
                return true;
            }

            // Two SocketExceptions describing the same native socket failure are the same error even
            // when they are distinct wrapper instances.
            return first is SocketException firstSocket
                && second is SocketException secondSocket
                && firstSocket.SocketErrorCode == secondSocket.SocketErrorCode;
        }

        internal void ReportAdditionalError(Exception error)
        {
            lock (_lifecycleGate)
            {
                RecordAdditionalError(error);
            }
        }

        /// <summary>
        /// Narrow per-instance observation boundary for stage tests: invoked on the completion
        /// thread once a send failure has been recognized (and, in the fixed implementation, its
        /// terminal state committed atomically) but <b>before</b> the result is published. Must be
        /// invoked outside the sender's directional gate. <see langword="null"/> in production.
        /// </summary>
        internal Action? BeforeSendFailurePublication { get; set; }

        /// <summary>
        /// Narrow per-instance observation boundary for stage tests: invoked on the pump thread
        /// immediately before the outbound source read is attempted (after the read has been
        /// reserved, in the fixed implementation), outside every gate. <see langword="null"/> in
        /// production.
        /// </summary>
        internal Action? BeforeSourcePopAttempt { get; set; }

        /// <summary>
        /// Narrow per-instance observation boundary for stage tests: invoked on the pump thread
        /// immediately before the send-post admission critical section for the current buffer range
        /// (a fresh chunk or a partial-send continuation), outside every gate. <see langword="null"/>
        /// in production.
        /// </summary>
        internal Action? BeforeSendPostAttempt { get; set; }

        /// <summary>
        /// Narrow per-instance observation boundary for stage tests: invoked on the notifying thread
        /// immediately before a <see cref="ITcpSocketSender.SourceDrained"/> handler's terminal
        /// admission check, outside every gate. The argument is the handler ordinal. <see langword="null"/>
        /// in production.
        /// </summary>
        internal Action<int>? BeforeDrainHandlerAdmission { get; set; }

        /// <summary>
        /// Narrow per-instance observation boundary for stage tests: invoked after the handler has
        /// been admitted and the gate released, immediately before the handler runs. The argument is
        /// the handler ordinal. <see langword="null"/> in production.
        /// </summary>
        internal Action<int>? BeforeDrainHandlerInvocation { get; set; }

        /// <summary>
        /// Narrow per-instance observation boundary for stage tests: invoked when a drain-notification
        /// request is queued while another notifier is active — the previous protocol's failed owner
        /// acquisition, where the pending write could be stranded. In the fixed protocol it runs after
        /// the request is atomically published. <see langword="null"/> in production.
        /// </summary>
        internal Action? OnDrainNotificationRequested { get; set; }

        /// <summary>
        /// Best-effort synchronous teardown for callers that do not need a bounded first return
        /// (<see cref="Dispose"/>, a directional fault). If a concurrent async/bounded disposal
        /// already queued the single reserved teardown, this returns immediately without waiting.
        /// </summary>
        private void ExecuteAbort()
        {
            if (!TryReserveTeardown())
            {
                return;
            }

            RunTeardown();
        }

        /// <summary>
        /// Reserves the connection's single physical teardown exactly once. Returns <see langword="false"/>
        /// when another path already queued/started/finished it, or when the connection already
        /// completed together with its exactly-once disposal attempt (nothing left to do).
        /// </summary>
        private bool TryReserveTeardown()
        {
            lock (_lifecycleGate)
            {
                if (_teardownReserved || _teardownStarted || _teardownFinished)
                {
                    return false;
                }

                if (_completionRecorded && _socketDisposeAttempted)
                {
                    return false;
                }

                _teardownReserved = true;
                return true;
            }
        }

        /// <summary>
        /// Reserves the single teardown and queues it to the internal executor without blocking the
        /// caller. No-op when the teardown was already reserved.
        /// </summary>
        private void QueueTeardown()
        {
            if (!TryReserveTeardown())
            {
                return;
            }

            _teardownExecutor.Queue(_teardownWork);
        }

        /// <summary>Marks the reserved teardown as started/finished around its best-effort body.</summary>
        private void RunTeardown()
        {
            lock (_lifecycleGate)
            {
                _teardownStarted = true;
            }

            try
            {
                ExecuteAbortCore();
            }
            finally
            {
                lock (_lifecycleGate)
                {
                    _teardownFinished = true;
                }
            }
        }

        /// <summary>
        /// The reserved physical teardown body. One failing stage must not skip the others or strand
        /// completion; every external failure is captured and classified through the regular
        /// diagnostic/result precedence rules.
        /// </summary>
        private void ExecuteAbortCore()
        {
            // Best-effort teardown: one failing stage must not skip the others or strand completion.
            try
            {
                _sender.Abort();
            }
            catch (Exception ex)
            {
                ReportAdditionalError(ex);
            }

            try
            {
                _receiver.Abort();
            }
            catch (Exception ex)
            {
                ReportAdditionalError(ex);
            }

            // Best-effort abort: benign outcomes (already disconnected/disposed) are ignored, but a
            // genuine shutdown failure must be surfaced rather than silently swallowed.
            Exception? shutdownError = TryShutdown(_socket, SocketShutdown.Both);
            if (shutdownError != null)
            {
                ReportAdditionalError(shutdownError);
            }

            // Exactly-once disposal stage; its failure is captured (not swallowed) and classified
            // when the aggregate result is created, after the attempt and error capture.
            TryDisposeSocket();

            TryFinalizeCompletion();
        }

        private void TryFinalizeCompletion()
        {
            SocketCompletionResult result;
            bool attemptDispose = false;

            lock (_lifecycleGate)
            {
                if (_completionRecorded || !_sendPublished || !_receivePublished)
                {
                    return;
                }

                if (!_socketDisposeAttempted)
                {
                    if (_abortRequested)
                    {
                        // Abort owns the disposal stage; it reruns finalization after its attempt.
                        return;
                    }

                    attemptDispose = true;
                }
            }

            if (attemptDispose)
            {
                TryDisposeSocket();
            }

            lock (_lifecycleGate)
            {
                if (_completionRecorded || !_sendPublished || !_receivePublished)
                {
                    return;
                }

                if (!_socketDisposeAttempted)
                {
                    // A concurrent teardown reserved the disposal attempt; it reruns finalization
                    // once the attempt completes.
                    return;
                }

                ClassifySocketDisposeErrorLocked();

                _completionRecorded = true;
                result = new SocketCompletionResult(
                    _primaryReason ?? SocketCompletionReason.Closed,
                    _primaryError,
                    _secondaryError,
                    _sendResult,
                    _receiveResult);
            }

            _completionSource.TrySetResult(result);
        }

        private void ReportCallbackError(Exception error)
        {
            if (_onCallbackError == null)
            {
                return;
            }

            try
            {
                _onCallbackError(error);
            }
            catch (Exception)
            {
                // diagnostic callbacks must not disrupt socket progress
            }
        }

        private static Exception? TryShutdown(ISocketAccessor socket, SocketShutdown direction)
        {
            try
            {
                socket.Shutdown(direction);
                return null;
            }
            catch (SocketException ex) when (
                ex.SocketErrorCode == SocketError.NotConnected ||
                ex.SocketErrorCode == SocketError.Shutdown ||
                ex.SocketErrorCode == SocketError.OperationAborted ||
                ex.SocketErrorCode == SocketError.ConnectionAborted)
            {
                return null;
            }
            catch (ObjectDisposedException)
            {
                // A concurrent teardown already disposed the socket; there is nothing to shut down.
                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }

        /// <summary>
        /// Reserves and performs the exactly-once accessor disposal attempt. Returns <see langword="true"/>
        /// when this call is the one that performed (or is performing) the attempt; <see langword="false"/>
        /// when another teardown already reserved it. No retries: repeated teardown must not duplicate
        /// disposal side effects.
        /// </summary>
        /// <remarks>
        /// The attempt runs outside <see cref="_lifecycleGate"/> so a slow accessor cannot block
        /// lifecycle bookkeeping. A captured failure (except a benign already-disposed outcome) is
        /// stored for classification when the aggregate result is created.
        /// </remarks>
        private bool TryDisposeSocket()
        {
            lock (_lifecycleGate)
            {
                if (_socketDisposeReserved)
                {
                    return false;
                }

                _socketDisposeReserved = true;
            }

            Exception? error = null;
            try
            {
                _socket.Dispose();
            }
            catch (ObjectDisposedException)
            {
                // Already disposed: benign per the accessor teardown contract.
            }
            catch (Exception ex)
            {
                error = ex;
            }

            bool lateDiagnostic;
            lock (_lifecycleGate)
            {
                _socketDisposeError = error;
                _socketDisposeAttempted = true;
                lateDiagnostic = _completionRecorded;
            }

            if (lateDiagnostic)
            {
                // The aggregate result was already forced/recorded (a deadline publication ran while
                // this disposal was blocked), so the failure can no longer enter it; report it
                // through the diagnostic channel exactly once.
                ReportSocketDisposeDiagnosticIfNeeded();
            }

            return true;
        }

        /// <summary>
        /// Classifies a captured disposal failure into the aggregate result being created. A normal
        /// close whose first failure is the disposal becomes <see cref="SocketCompletionReason.Faulted"/>;
        /// an established reason (local <see cref="SocketCompletionReason.Disposed"/>, a forced reason,
        /// or an I/O fault) keeps first-terminal precedence and the disposal failure goes to the single
        /// secondary slot.
        /// </summary>
        private void ClassifySocketDisposeErrorLocked()
        {
            Exception? disposalError = _socketDisposeError;
            if (disposalError == null)
            {
                return;
            }

            if (_primaryReason == null)
            {
                _primaryReason = SocketCompletionReason.Faulted;
                _primaryError = disposalError;
                return;
            }

            RecordAdditionalError(disposalError);
        }

        /// <summary>
        /// Reports a captured disposal failure that could not enter an already-forced/immutable result
        /// to the best-effort diagnostic callback, at most once. A throwing diagnostic callback cannot
        /// disrupt teardown (see <see cref="ReportCallbackError"/>).
        /// </summary>
        private void ReportSocketDisposeDiagnosticIfNeeded()
        {
            Exception? disposalError;
            lock (_lifecycleGate)
            {
                if (_socketDisposeDiagnosticReported || _socketDisposeError == null)
                {
                    return;
                }

                _socketDisposeDiagnosticReported = true;
                disposalError = _socketDisposeError;
            }

            ReportCallbackError(disposalError);
        }

        private sealed class SenderImpl : ITcpSocketSender
        {
            private enum SenderState
            {
                Open,
                GracefulShutdownRequested,
                ImmediateShutdownRequested
            }

            private readonly LowLevelTcpSocket _owner;
            private readonly ISocketAccessor _socket;
            private readonly ISingleReaderWriterConcurrentQueue<IMultiRefByteArray> _source;

            private const int MaxWouldBlockRetries = 100;

            private readonly SocketAsyncEventArgs _args;
            private readonly object _gate = new();
            private readonly TaskCompletionSource<SenderCompletionResult> _completionSource =
                new(TaskCreationOptions.RunContinuationsAsynchronously);

            // Resolves when the transport has permanently stopped reading the source. Independent of
            // logical Completion: Completion may publish while a source read is still in flight.
            private readonly TaskCompletionSource<bool> _sourceQuiescedSource =
                new(TaskCreationOptions.RunContinuationsAsynchronously);

            private bool _sourceReadReserved;

            private IMultiRefByteArray? _currentBuffer;
            private int _sentBytes;
            private int _wouldBlockRetries;
            private bool _pending;
            private bool _pumping;
            private bool _pumpRequested;
            private bool _cleanedUp;
            private bool _sourceDrainedPublished;
            private readonly object _notifyGate = new();
            private bool _notifyRunning;
            private bool _notifyPending;
            private readonly IDeadlineScheduler _deadlineScheduler;

            // Deadline state: protected by _gate (generation/due/armed/expired) with _deadlineSync
            // serializing the scheduler operations themselves so a slow/reentrant timer op never
            // runs under the I/O gate and two arms cannot reorder their Schedule calls.
            private readonly object _deadlineSync = new();
            private int _deadlineGeneration;
            private bool _deadlineArmed;
            private long _deadlineStartTimestamp;
            private TimeSpan _deadlineDuration;

            // Persistent lifecycle state, set under _gate by the deadline callback when the
            // whole-shutdown deadline elapses and the sender is not yet terminal. Once set, no
            // further source read or send post may be admitted and the unfinished logical shutdown
            // is force-published. It is never reset: a terminal sender or a new instance is the
            // only exit.
            private bool _deadlineExpired;
            private bool _cancelDeadlineOnPublish;
            private SenderState _state;
            private SenderCompletionResult? _terminalResult;

            // Cleanup failures (buffer release, args disposal, timer disposal) are captured here and
            // reported only after the terminal result has reserved the initiating fault's aggregate
            // precedence, so they can never displace the primary error. Guarded by _gate.
            private readonly List<Exception> _cleanupErrors = new();

            // Set by TryCleanup when the reserved terminal result requires an owner abort. Executed
            // by PublishTermination outside _gate (never while a directional gate is held).
            private bool _abortAfterCleanup;

            // Resources detached from the steady-state fields by the exactly-once cleanup
            // reservation (TryCleanup) and released/disposed outside _gate by ExecuteCleanup
            // (invoked from PublishTermination). Release and DisposeEventArgs are external calls: a
            // blocked pool return or args lifetime adapter must not hold the I/O gate or delay
            // abort state admission (C05/P06).
            private IMultiRefByteArray? _cleanupBuffer;
            private bool _cleanupArgsDetached;
            private bool _cleanupExecuted;

            public Task<SenderCompletionResult> Completion => _completionSource.Task;

            public Task SourceQuiesced => _sourceQuiescedSource.Task;

            // Immutable subscriber snapshot rebuilt on subscription changes; delivery reads the
            // current snapshot once and iterates it without allocating (P03). Standard .NET event
            // semantics: duplicates run multiple times and Remove removes one (last) occurrence.
            private readonly object _handlersGate = new();
            private Action[] _sourceDrainedHandlers = Array.Empty<Action>();
            private readonly Action<Exception> _reportCallbackError;

            public event Action? SourceDrained
            {
                add
                {
                    if (value == null)
                    {
                        return;
                    }

                    lock (_handlersGate)
                    {
                        Action[] current = _sourceDrainedHandlers;
                        var updated = new Action[current.Length + 1];
                        Array.Copy(current, updated, current.Length);
                        updated[current.Length] = value;
                        Volatile.Write(ref _sourceDrainedHandlers, updated);
                    }
                }
                remove
                {
                    if (value == null)
                    {
                        return;
                    }

                    lock (_handlersGate)
                    {
                        Action[] current = _sourceDrainedHandlers;
                        int index = Array.LastIndexOf(current, value);
                        if (index < 0)
                        {
                            return;
                        }

                        var updated = new Action[current.Length - 1];
                        Array.Copy(current, updated, index);
                        Array.Copy(current, index + 1, updated, index, current.Length - index - 1);
                        Volatile.Write(ref _sourceDrainedHandlers, updated);
                    }
                }
            }

            public SenderImpl(
                LowLevelTcpSocket owner,
                ISocketAccessor socket,
                ISingleReaderWriterConcurrentQueue<IMultiRefByteArray> source,
                IDeadlineScheduler deadlineScheduler)
            {
                _owner = owner;
                _socket = socket;
                _source = source;
                _deadlineScheduler = deadlineScheduler;
                _reportCallbackError = owner.ReportCallbackError;

                _args = socket.CreateEventArgs();
                try
                {
                    _args.Completed += OnCompleted;
                    _args.SocketFlags = SocketFlags.None;
                }
                catch
                {
                    RollbackConstruction();
                    throw;
                }
            }

            /// <summary>
            /// Releases the directional args when the owning socket's construction failed. Performs
            /// only unsubscribe/dispose: it must not publish a result or assume the other direction
            /// exists, and a rollback failure is swallowed so it cannot mask the initiating
            /// construction exception.
            /// </summary>
            internal void RollbackConstruction()
            {
                if (_args == null)
                {
                    return;
                }

                try
                {
                    _args.Completed -= OnCompleted;
                    _socket.DisposeEventArgs(_args);
                }
                catch (Exception)
                {
                    // Best effort: the initiating failure is already propagating.
                }
            }

            public void Pump()
            {
                _owner.ThrowIfDisposed();

                Exception? error = null;
                bool drained = false;
                bool ended = false;

                if (PumpSend(ref error, ref drained, ref ended))
                {
                    PublishSourceDrained();
                }
            }

            public void Shutdown(bool isGraceful)
            {
                _owner.ThrowIfDisposed();
                ShutdownCore(isGraceful, Timeout.InfiniteTimeSpan, useConstructorDefault: true);
            }

            public void Shutdown(bool isGraceful, TimeSpan deadline)
            {
                _owner.ThrowIfDisposed();
                ValidateDeadline(deadline, nameof(deadline));
                ShutdownCore(isGraceful, deadline);
            }

            // Internal entry point used by the owner (e.g. the remote half-close policy) that must keep
            // working during teardown and therefore bypasses the disposed-state guard.
            internal void ShutdownCore(bool isGraceful, TimeSpan deadline)
                => ShutdownCore(isGraceful, deadline, useConstructorDefault: false);

            internal void ShutdownCore(bool isGraceful, TimeSpan deadline, bool useConstructorDefault)
            {
                Exception? error = null;
                bool drained = false;
                bool ended = false;
                bool shouldPump = false;
                bool armDeadline = false;
                bool cancelDeadline = false;
                TimeSpan armDuration = default;

                lock (_gate)
                {
                    if (IsTerminal)
                    {
                        // A terminal sender is never revived and must not allocate/rearm a timer.
                        return;
                    }

                    if (useConstructorDefault)
                    {
                        // The constructor graceful deadline is the default for the first parameterless
                        // graceful request and the remote-EOF policy; repeated requests and immediate
                        // escalation preserve an already-established deadline (no extension).
                        if (isGraceful
                            && !_deadlineArmed
                            && _owner._gracefulShutdownDeadline.HasValue
                            && _owner._gracefulShutdownDeadline.Value != Timeout.InfiniteTimeSpan)
                        {
                            armDeadline = true;
                            armDuration = _owner._gracefulShutdownDeadline.Value;
                        }
                    }
                    else if (deadline == Timeout.InfiniteTimeSpan)
                    {
                        cancelDeadline = _deadlineArmed;
                    }
                    else
                    {
                        armDeadline = true;
                        armDuration = deadline;
                    }

                    if (_state == SenderState.GracefulShutdownRequested)
                    {
                        if (!isGraceful)
                        {
                            _state = SenderState.ImmediateShutdownRequested;
                            shouldPump = true;
                        }
                    }
                    else if (_state == SenderState.Open)
                    {
                        _state = isGraceful
                            ? SenderState.GracefulShutdownRequested
                            : SenderState.ImmediateShutdownRequested;
                        shouldPump = true;
                    }
                }

                if (cancelDeadline)
                {
                    CancelDeadline();
                }

                if (armDeadline)
                {
                    ArmDeadline(armDuration);
                }

                if (!shouldPump)
                {
                    return;
                }

                if (PumpSend(ref error, ref drained, ref ended))
                {
                    PublishSourceDrained();
                }
            }

            /// <summary>
            /// Installs or replaces the one-shot shutdown deadline. The generation is bumped under
            /// <see cref="_gate"/> and the scheduler operation is serialized by <see cref="_deadlineSync"/>,
            /// so an already-dispatched callback can never act for a replaced/cancelled deadline and two
            /// arms cannot reorder their scheduler calls. No I/O gate is held across the timer op.
            /// </summary>
            private void ArmDeadline(TimeSpan duration)
            {
                lock (_deadlineSync)
                {
                    int generation;
                    lock (_gate)
                    {
                        if (IsTerminal || _deadlineExpired)
                        {
                            // never allocate/rearm for a terminal sender, and never revive a
                            // deadline whose expiry has already won the whole-drain race
                            return;
                        }

                        _deadlineGeneration++;
                        generation = _deadlineGeneration;
                        _deadlineArmed = true;
                        _deadlineStartTimestamp = _deadlineScheduler.GetTimestamp();
                        _deadlineDuration = duration;
                    }

                    _deadlineScheduler.Schedule(duration, () => OnShutdownDeadline(generation));
                }
            }

            /// <summary>
            /// Invalidates the current deadline generation and cancels the pending callback. The
            /// generation bump happens under <see cref="_gate"/> (only a few field writes); the
            /// scheduler cancellation happens under <see cref="_deadlineSync"/> only.
            /// </summary>
            private void CancelDeadline()
            {
                lock (_deadlineSync)
                {
                    lock (_gate)
                    {
                        _deadlineGeneration++;
                        _deadlineArmed = false;
                    }

                    _deadlineScheduler.Cancel();
                }
            }

            private void OnShutdownDeadline(int generation)
            {
                bool expired = false;
                TimeSpan remaining = default;

                lock (_gate)
                {
                    if (!_deadlineArmed || generation != _deadlineGeneration || IsTerminal)
                    {
                        return; // obsolete generation or terminal sender: ignore
                    }

                    remaining = _deadlineDuration - _deadlineScheduler.GetElapsedTime(_deadlineStartTimestamp);
                    if (remaining <= TimeSpan.Zero)
                    {
                        // The deadline covers the entire shutdown/drain, not one operation: expiry is
                        // persistent lifecycle state (not a transient _pending observation). Once set
                        // under _gate, no source read or send post may be admitted, and the unfinished
                        // logical shutdown is force-published below, outside this gate.
                        _deadlineExpired = true;
                        expired = true;
                    }
                }

                if (expired)
                {
                    _owner.ForceAbortFinalize(new TimeoutException("Send shutdown deadline exceeded."));
                    return;
                }

                if (remaining > TimeSpan.Zero)
                {
                    // A callback that fired early is re-scheduled for the remainder.
                    lock (_deadlineSync)
                    {
                        lock (_gate)
                        {
                            if (!_deadlineArmed || generation != _deadlineGeneration || IsTerminal)
                            {
                                return;
                            }
                        }

                        _deadlineScheduler.Schedule(remaining, () => OnShutdownDeadline(generation));
                    }
                }
            }

            internal void Abort()
            {
                bool publish;
                SenderCompletionResult result;

                lock (_gate)
                {
                    TrySetTerminal(new SenderCompletionResult(SenderCompletionReason.Aborted, null));
                    publish = TryCleanup(out result);
                }

                if (publish)
                {
                    PublishTermination(result);
                }
            }

            /// <summary>
            /// Escape-hatch publication used by a bounded shutdown when the native operation will not
            /// complete. The in-flight buffer and event args are abandoned here, not released, because
            /// the runtime may still reference them. Physical cleanup is <b>not</b> marked complete:
            /// a later native completion (or pump recheck) must still be able to reclaim the resources
            /// exactly once via <see cref="TryCleanup"/>.
            /// </summary>
            internal void ForcePublish(Exception? error)
            {
                SenderCompletionResult result;

                lock (_gate)
                {
                    if (!_terminalResult.HasValue)
                    {
                        _terminalResult = new SenderCompletionResult(SenderCompletionReason.Aborted, error);
                    }

                    // Terminal is now established; the quiescence publication still waits for any
                    // reserved/active source read (the check is atomic under this gate).
                    TryPublishSourceQuiescedLocked();

                    // Do NOT set _cleanedUp here: publication and physical reclamation are separate
                    // facts, and TryCleanup remains the sole exactly-once cleanup reservation.
                    _pumpRequested = false;
                    _deadlineGeneration++;
                    _deadlineArmed = false;

                    result = _terminalResult.Value;
                }

                // The timer op runs outside the I/O gate.
                try
                {
                    _deadlineScheduler.Cancel();
                }
                catch (Exception ex)
                {
                    CaptureCleanupError(ex);
                }

                _owner.RegisterSendTermination(result);
                FlushCleanupErrors();
                _completionSource.TrySetResult(result);
                _owner.MarkSendPublished();
            }

            private void OnCompleted(object? sender, SocketAsyncEventArgs args)
            {
                Exception? error = null;
                bool drained = false;
                bool ended = false;
                bool deferred = false;
                bool terminal = false;
                bool publish = false;
                SenderCompletionResult result = default;

                lock (_gate)
                {
                    _pending = false;

                    if (IsTerminal)
                    {
                        publish = TryCleanup(out result);
                        terminal = true;
                    }
                    else if (args.SocketError == SocketError.WouldBlock)
                    {
                        if (++_wouldBlockRetries > MaxWouldBlockRetries)
                        {
                            error = new SocketException((int)SocketError.WouldBlock);
                        }
                        else if (_pumping)
                        {
                            _pumpRequested = true;
                            deferred = true;
                        }
                    }
                    else if (args.SocketError != SocketError.Success)
                    {
                        error = new SocketException((int)args.SocketError);
                    }
                    else
                    {
                        int transferred = args.BytesTransferred;
                        if (transferred <= 0)
                        {
                            error = new SendCompletedWithoutProgressException(
                                args.SocketError, args.Offset, args.Count, _sentBytes);
                        }
                        else
                        {
                            _sentBytes += transferred;
                            _wouldBlockRetries = 0;

                            if (_pumping)
                            {
                                _pumpRequested = true;
                                deferred = true;
                            }
                        }
                    }

                    // Commit the recognized terminal failure atomically with the cleared pending
                    // state, under the same critical section that classified it. A competing pump
                    // must never observe "open + not pending" after failure recognition.
                    if (!terminal && error != null)
                    {
                        TrySetTerminal(new SenderCompletionResult(SenderCompletionReason.Faulted, error));
                        publish = TryCleanup(out result);
                    }
                }

                // Narrow observation boundary: the failure is committed but not yet published.
                if (error != null)
                {
                    _owner.BeforeSendFailurePublication?.Invoke();
                }

                if (terminal || error != null)
                {
                    if (publish)
                    {
                        PublishTermination(result);
                    }

                    return;
                }

                if (!deferred)
                {
                    if (PumpSend(ref error, ref drained, ref ended))
                    {
                        PublishSourceDrained();
                    }
                }
            }

            private bool PumpSend(ref Exception? error, ref bool drained, ref bool ended)
            {
                lock (_gate)
                {
                    if (IsTerminal)
                    {
                        return false;
                    }

                    if (_pumping)
                    {
                        _pumpRequested = true;
                        return false;
                    }

                    _pumping = true;
                }

                while (true)
                {
                    bool endRequested = false;
                    try
                    {
                        PumpLoop(ref error, ref drained, ref ended, ref endRequested);
                    }
                    catch (Exception ex)
                    {
                        error = ex;
                    }

                    // The drained/immediate end-of-send socket shutdown runs outside _gate: the pump
                    // still owns the send direction (a worker is inside PumpSend), so no new post can
                    // be admitted while the external call is in flight, and a concurrent abort that
                    // wins the gate meanwhile keeps first-terminal precedence.
                    if (endRequested && error == null)
                    {
                        error = EndSendOutsideGate();
                        ended = error == null;
                    }

                    bool resume;
                    bool publish;
                    bool notifySourceDrained;
                    SenderCompletionResult result;
                    lock (_gate)
                    {
                        _pumping = false;

                        if (error != null)
                        {
                            TrySetTerminal(new SenderCompletionResult(SenderCompletionReason.Faulted, error));
                        }

                        publish = TryCleanup(out result);

                        resume = _pumpRequested && !IsTerminal && error == null;
                        if (resume)
                        {
                            _pumpRequested = false;
                            _pumping = true;
                            drained = false;
                        }

                        notifySourceDrained = !resume && drained && !IsTerminal && !_sourceDrainedPublished;
                        _sourceDrainedPublished |= notifySourceDrained;
                    }

                    if (publish)
                    {
                        PublishTermination(result);
                    }

                    if (!resume)
                    {
                        return notifySourceDrained;
                    }
                }
            }

            private void PumpLoop(ref Exception? error, ref bool drained, ref bool ended, ref bool endRequested)
            {
                while (true)
                {
                    bool needPop = false;

                    lock (_gate)
                    {
                        if (IsTerminal)
                        {
                            return;
                        }

                        if (_deadlineExpired)
                        {
                            // The whole-drain deadline won the admission race. The forced publication
                            // owns terminal state and cleanup; starting a source read here would only
                            // hand back a buffer that must be immediately released.
                            return;
                        }

                        if (_pending)
                        {
                            return;
                        }

                        if (_state == SenderState.ImmediateShutdownRequested)
                        {
                            ReleaseCurrentBuffer();
                            endRequested = true;
                            return;
                        }

                        if (_currentBuffer != null && _sentBytes >= _currentBuffer.Count)
                        {
                            ReleaseCurrentBuffer();

                            // ReleaseCurrentBuffer runs the pool return outside the gate, so the
                            // terminal state or the whole-drain deadline may have been established
                            // meanwhile (SourceQuiesced may already be published). Re-validate before
                            // reserving another source read.
                            if (IsTerminal || _deadlineExpired)
                            {
                                return;
                            }
                        }

                        if (_currentBuffer == null)
                        {
                            // Reserve the source read under the same gate that validated the
                            // non-terminal state, before leaving it to call TryPop. A pump that
                            // decided to pop before an abort still owns this reservation, so
                            // SourceQuiesced cannot publish until the read finishes.
                            _sourceReadReserved = true;
                            needPop = true;
                        }
                    }

                    if (needPop)
                    {
                        try
                        {
                            _owner.BeforeSourcePopAttempt?.Invoke();

                            if (!_source.TryPop(out var buffer))
                            {
                                bool recheck;
                                lock (_gate)
                                {
                                    if (IsTerminal)
                                    {
                                        return;
                                    }

                                    if (_deadlineExpired)
                                    {
                                        // Expiry owns termination; do not conclude a graceful drain or
                                        // start another read.
                                        return;
                                    }

                                // A shutdown request may have arrived after our empty observation, and a
                                // pump may have been requested because data was published meanwhile. The empty
                                // observation is then stale: re-check the source before concluding the drain.
                                recheck = _pumpRequested;
                                _pumpRequested = false;

                                if (!recheck)
                                {
                                    if (_state != SenderState.Open)
                                    {
                                        endRequested = true;
                                    }
                                    else
                                    {
                                        drained = true;
                                    }

                                    return;
                                }
                            }

                            continue;
                        }

                        if (ReferenceEquals(buffer, null))
                        {
                            error = new InvalidOperationException("Send source produced a null buffer.");
                            return;
                        }

                        lock (_gate)
                        {
                            if (IsTerminal || _deadlineExpired)
                            {
                                // A buffer popped after terminal/expiry is never posted; release it
                                // before the reservation clears so SourceQuiesced implies disposition.
                                // The release runs outside the gate (a late, arbitrary pool return can
                                // block) and the caller's critical section resumes afterwards.
                                ReleaseOutsideGate(buffer);
                                return;
                            }

                            _currentBuffer = buffer;
                            _sentBytes = 0;
                            _sourceDrainedPublished = false;
                        }
                        }
                        finally
                        {
                            lock (_gate)
                            {
                                if (_sourceReadReserved)
                                {
                                    _sourceReadReserved = false;
                                    TryPublishSourceQuiescedLocked();
                                }
                            }
                        }
                    }

                    // Narrow observation boundary: the pump is about to admit (or reject) the next
                    // send post for the current buffer range; runs outside every gate.
                    _owner.BeforeSendPostAttempt?.Invoke();

                    lock (_gate)
                    {
                        if (IsTerminal)
                        {
                            return;
                        }

                        if (_pending)
                        {
                            return;
                        }

                        if (_deadlineExpired)
                        {
                            // Expiry linearizes with the post admission: no further chunk or partial
                            // continuation may be posted once the deadline has won under _gate.
                            return;
                        }

                        if (_state == SenderState.ImmediateShutdownRequested)
                        {
                            ReleaseCurrentBuffer();
                            endRequested = true;
                            return;
                        }

                        var current = _currentBuffer;
                        if (current == null)
                        {
                            continue;
                        }

                        int remaining = current.Count - _sentBytes;
                        if (remaining <= 0)
                        {
                            continue;
                        }

                        _args.SetBuffer(current.Array, current.Offset + _sentBytes, remaining);

                        if (_socket.SendAsync(_args))
                        {
                            _pending = true;
                            return;
                        }

                        if (_args.SocketError == SocketError.WouldBlock)
                        {
                            if (++_wouldBlockRetries > MaxWouldBlockRetries)
                            {
                                error = new SocketException((int)SocketError.WouldBlock);
                                return;
                            }

                            Thread.Yield();
                            continue;
                        }

                        if (_args.SocketError != SocketError.Success)
                        {
                            error = new SocketException((int)_args.SocketError);
                            return;
                        }

                        int transferred = _args.BytesTransferred;
                        if (transferred <= 0)
                        {
                            error = new SendCompletedWithoutProgressException(
                                _args.SocketError, _args.Offset, _args.Count, _sentBytes);
                            return;
                        }

                        _sentBytes += transferred;
                        _wouldBlockRetries = 0;
                    }
                }
            }

            /// <summary>
            /// Ends a drained/immediately-shutdown send direction by shutting the socket's send half
            /// down. Runs outside <see cref="_gate"/>: this is an external accessor call that can
            /// block, and holding the I/O gate across it would stall abort admission (C05/P06). The
            /// pump still owns the direction while this runs (a worker is inside
            /// <see cref="PumpSend"/>), so no new post can be admitted. If a concurrent abort/force
            /// wins terminal state meanwhile, the late graceful/immediate result is discarded
            /// (first-terminal-wins).
            /// </summary>
            private Exception? EndSendOutsideGate()
            {
                lock (_gate)
                {
                    if (IsTerminal)
                    {
                        return null; // a concurrent abort/force already owns termination
                    }
                }

                Exception? error = TryShutdown(_socket, SocketShutdown.Send);

                lock (_gate)
                {
                    if (IsTerminal)
                    {
                        return null;
                    }

                    SenderCompletionReason reason = _state == SenderState.GracefulShutdownRequested
                        ? SenderCompletionReason.GracefulShutdown
                        : SenderCompletionReason.ImmediateShutdown;
                    TrySetTerminal(error == null
                        ? new SenderCompletionResult(reason, null)
                        : new SenderCompletionResult(SenderCompletionReason.Faulted, error));
                }

                return error;
            }

            /// <summary>
            /// Invokes one immutable subscriber snapshot with per-handler terminal admission. Each
            /// handler is admitted (or rejected) under <see cref="_gate"/>, and the invocation itself
            /// runs outside every gate so user code cannot block I/O or teardown. An already-admitted
            /// handler may run later; no handler is admitted once terminal. Iterating the snapshot by
            /// index allocates nothing per delivery (P03); subscription changes allocate at
            /// subscription time and affect subsequent snapshots only.
            /// </summary>
            private void InvokeSafely(Action[] handlers, Action<Exception> onError)
            {
                for (int index = 0; index < handlers.Length; index++)
                {
                    _owner.BeforeDrainHandlerAdmission?.Invoke(index);

                    lock (_gate)
                    {
                        if (IsTerminal)
                        {
                            return;
                        }
                    }

                    _owner.BeforeDrainHandlerInvocation?.Invoke(index);

                    try
                    {
                        handlers[index]();
                    }
                    catch (Exception ex)
                    {
                        onError(ex);
                    }
                }
            }

            private void PublishSourceDrained()
            {
                // Request publication and the ownership/idle transition are one coherent protocol:
                // under _notifyGate a request cannot slip in between the owner's "no pending work"
                // decision and its release to idle. At most one notifier runs; multiple requests
                // coalesce into one pending delivery (C08).
                bool becomeNotifier;
                lock (_notifyGate)
                {
                    _notifyPending = true;
                    becomeNotifier = !_notifyRunning;
                    if (becomeNotifier)
                    {
                        _notifyRunning = true;
                    }
                }

                if (!becomeNotifier)
                {
                    // The active notifier will observe the published request before going idle.
                    _owner.OnDrainNotificationRequested?.Invoke();
                    return;
                }

                try
                {
                    while (true)
                    {
                        lock (_notifyGate)
                        {
                            if (!_notifyPending)
                            {
                                _notifyRunning = false;
                                return;
                            }

                            _notifyPending = false;
                        }

                        if (!IsTerminalLocked())
                        {
                            InvokeSafely(Volatile.Read(ref _sourceDrainedHandlers), _reportCallbackError);
                        }
                    }
                }
                catch
                {
                    lock (_notifyGate)
                    {
                        _notifyRunning = false;
                    }

                    throw;
                }
            }

            private bool IsTerminalLocked()
            {
                lock (_gate)
                {
                    return IsTerminal;
                }
            }

            private void ReleaseCurrentBuffer()
            {
                var buffer = _currentBuffer;
                _currentBuffer = null;
                _sentBytes = 0;

                // The completed buffer is detached and no native operation references the event args
                // whenever this runs (_pending is false under the caller's gate), so the stale array
                // root in the args must be cleared (P01). This does not revoke consumer-owned storage.
                ClearArgsBuffer();

                if (buffer == null)
                {
                    return;
                }

                ReleaseOutsideGate(buffer);
            }

            /// <summary>
            /// Clears the event-args buffer reference so a released array is not retained by the idle
            /// args. Callers must guarantee no native operation references the args (never clear while
            /// an operation owns them, e.g. while <c>_pending</c>); partial progress is preserved by
            /// re-setting the remaining range at the next post. Cheap O(1) field reset under the gate.
            /// </summary>
            private void ClearArgsBuffer()
            {
                _args.SetBuffer(null, 0, 0);
            }

            /// <summary>
            /// Releases a detached buffer with <see cref="_gate"/> temporarily released, so a slow or
            /// blocked pool return cannot stall abort state admission or completion (C05/P06). Must be
            /// called while holding <see cref="_gate"/> exactly once; the critical section resumes
            /// afterwards on the same thread. The buffer is already detached from the connection's
            /// state, so no other thread can observe or reclaim it while it is being released.
            /// </summary>
            private void ReleaseOutsideGate(IMultiRefByteArray buffer)
            {
                Monitor.Exit(_gate);
                try
                {
                    buffer.Release();
                }
                catch (Exception ex)
                {
                    CaptureCleanupError(ex);
                }
                finally
                {
                    Monitor.Enter(_gate);
                }
            }

            private void CaptureCleanupError(Exception error)
            {
                lock (_gate)
                {
                    _cleanupErrors.Add(error);
                }
            }

            private void FlushCleanupErrors()
            {
                Exception[] errors;
                lock (_gate)
                {
                    if (_cleanupErrors.Count == 0)
                    {
                        return;
                    }

                    errors = _cleanupErrors.ToArray();
                    _cleanupErrors.Clear();
                }

                foreach (Exception error in errors)
                {
                    _owner.ReportAdditionalError(error);
                }
            }

            private bool TryCleanup(out SenderCompletionResult result)
            {
                // A pump that is merely blocked inside the external source TryPop does not touch the
                // event args or the in-flight buffer: every such access happens under _gate and
                // re-checks IsTerminal, so publishing must not be held hostage by it. Any buffer that
                // is popped after termination is released by the pump itself when it re-checks state.
                if (_cleanedUp || !IsTerminal || _pending)
                {
                    result = default;
                    return false;
                }

                // Reservation first: even if a cleanup stage fails, publication must still happen.
                // Only the in-flight buffer is the transport's to release; queued-but-unsent buffers
                // remain the producer's and are drained by it after Sender.SourceQuiesced resolves.
                _cleanedUp = true;

                // Reserve the terminal result (and the initiating fault's aggregate precedence)
                // before any external cleanup runs. The abort itself is deferred to
                // PublishTermination: executing it here would recursively take directional gates
                // while _gate is held, risking a cross-direction deadlock.
                result = _terminalResult!.Value;
                _abortAfterCleanup = _owner.ReserveSendTermination(result);

                // Detach the resources here; the actual external Release/DisposeEventArgs work runs
                // in ExecuteCleanup (via PublishTermination) outside _gate, so a blocked pool return
                // or args adapter cannot hold the gate or delay abort state admission.
                _cleanupBuffer = _currentBuffer;
                _currentBuffer = null;
                _sentBytes = 0;
                _cleanupArgsDetached = true;

                // No native operation references the args here (TryCleanup requires !_pending): clear
                // the released array root before the args are eventually disposed (P01).
                ClearArgsBuffer();

                // Invalidate the deadline generation under the gate and defer the scheduler op to
                // PublishTermination (outside the gate).
                _deadlineGeneration++;
                _deadlineArmed = false;
                _cancelDeadlineOnPublish = true;

                return true;
            }

            /// <summary>
            /// Performs the reserved exactly-once external cleanup: releases the detached in-flight
            /// buffer and disposes the directional event args. Runs outside every gate; failures are
            /// captured and reported after the initiating terminal result has reserved aggregate
            /// precedence. Called exactly once per cleanup reservation (by
            /// <see cref="PublishTermination"/>).
            /// </summary>
            private void ExecuteCleanup()
            {
                IMultiRefByteArray? buffer;
                bool detachArgs;

                lock (_gate)
                {
                    if (_cleanupExecuted)
                    {
                        return;
                    }

                    _cleanupExecuted = true;
                    buffer = _cleanupBuffer;
                    _cleanupBuffer = null;
                    detachArgs = _cleanupArgsDetached;
                    _cleanupArgsDetached = false;
                }

                if (buffer != null)
                {
                    try
                    {
                        buffer.Release();
                    }
                    catch (Exception ex)
                    {
                        CaptureCleanupError(ex);
                    }
                }

                if (detachArgs)
                {
                    try
                    {
                        _args.Completed -= OnCompleted;
                        _socket.DisposeEventArgs(_args);
                    }
                    catch (Exception ex)
                    {
                        CaptureCleanupError(ex);
                    }
                }
            }

            private void PublishTermination(SenderCompletionResult result)
            {
                // Physical cleanup of the reserved resources happens here, outside every gate,
                // before the logical result is published: normal completion must still wait for its
                // reserved cleanup attempts (a blocked cleanup delays publication, not admission).
                ExecuteCleanup();

                bool abort = _abortAfterCleanup;
                _abortAfterCleanup = false;

                FlushCleanupErrors();

                if (_cancelDeadlineOnPublish)
                {
                    _cancelDeadlineOnPublish = false;
                    try
                    {
                        _deadlineScheduler.Cancel();
                    }
                    catch (Exception ex)
                    {
                        _owner.ReportAdditionalError(ex);
                    }
                }

                if (abort)
                {
                    _owner.ExecuteAbort();
                }

                _completionSource.TrySetResult(result);
                _owner.MarkSendPublished();
            }

            private bool TrySetTerminal(SenderCompletionResult result)
            {
                if (_terminalResult.HasValue)
                {
                    return false;
                }

                _terminalResult = result;
                _pumpRequested = false;
                TryPublishSourceQuiescedLocked();
                return true;
            }

            /// <summary>
            /// Publishes <see cref="SourceQuiesced"/> once the sender is terminal and no source read
            /// is reserved or active. Must be called under <see cref="_gate"/>: the decision that no
            /// new reservation can occur is atomic with the publication. Late-popped buffers are
            /// released before the reservation is cleared, so the acknowledgment also implies their
            /// disposition.
            /// </summary>
            private void TryPublishSourceQuiescedLocked()
            {
                if (IsTerminal && !_sourceReadReserved)
                {
                    _sourceQuiescedSource.TrySetResult(true);
                }
            }

            private bool IsTerminal => _terminalResult.HasValue;
        }

        private sealed class ReceiverImpl : ITcpSocketReceiver
        {
            private enum ReceiverState
            {
                Created,
                Running
            }

            private readonly LowLevelTcpSocket _owner;
            private readonly ISocketAccessor _socket;
            private readonly Action<IMultiRefByteArray> _onReceived;
            private readonly IConcurrentPool<IMultiRefByteArray, int> _pool;
            private readonly SocketAsyncEventArgs _args;
            private readonly int _bufferSize;
            private readonly object _gate = new();
            private readonly TaskCompletionSource<ReceiverCompletionResult> _completionSource =
                new(TaskCreationOptions.RunContinuationsAsynchronously);

            private const int MaxWouldBlockRetries = 100;
            private IMultiRefByteArray? _currentBuffer;
            private int _wouldBlockRetries;
            private int _activeDispatches;
            private bool _pending;
            private bool _pumping;
            private bool _cleanedUp;
            private ReceiverState _state;
            private ReceiverCompletionResult? _terminalResult;

            // Reserved by Shutdown() before it leaves the gate for the external socket shutdown, so
            // no new receive can be admitted while the accessor call is in flight and a concurrent
            // abort can establish first-terminal precedence. Cleared when the call returns.
            private bool _shutdownReserved;

            // Cleanup failures (buffer release, args disposal) are captured here and reported only
            // after the terminal result has reserved the initiating fault's aggregate precedence, so
            // they can never displace the primary error. Guarded by _gate.
            private readonly List<Exception> _cleanupErrors = new();

            // Set by TryCleanup when the reserved terminal result requires an owner abort. Executed
            // by PublishTermination outside _gate (never while a directional gate is held).
            private bool _abortAfterCleanup;

            // Resources detached by the exactly-once cleanup reservation (TryCleanup) and
            // released/disposed outside _gate by ExecuteCleanup (via PublishTermination).
            private IMultiRefByteArray? _cleanupBuffer;
            private bool _cleanupArgsDetached;
            private bool _cleanupExecuted;

            public Task<ReceiverCompletionResult> Completion => _completionSource.Task;

            public ReceiverImpl(
                LowLevelTcpSocket owner,
                ISocketAccessor socket,
                Action<IMultiRefByteArray> onReceived,
                IConcurrentPool<IMultiRefByteArray, int> pool,
                int bufferSize)
            {
                _owner = owner;
                _socket = socket;
                _onReceived = onReceived;
                _pool = pool;
                _bufferSize = bufferSize;

                _args = socket.CreateEventArgs();
                try
                {
                    _args.Completed += OnCompleted;
                    _args.SocketFlags = SocketFlags.None;
                }
                catch
                {
                    RollbackConstruction();
                    throw;
                }
            }

            /// <summary>
            /// Releases the directional args when the owning socket's construction failed. Performs
            /// only unsubscribe/dispose: it must not publish a result or assume the other direction
            /// exists, and a rollback failure is swallowed so it cannot mask the initiating
            /// construction exception.
            /// </summary>
            internal void RollbackConstruction()
            {
                if (_args == null)
                {
                    return;
                }

                try
                {
                    _args.Completed -= OnCompleted;
                    _socket.DisposeEventArgs(_args);
                }
                catch (Exception)
                {
                    // Best effort: the initiating failure is already propagating.
                }
            }

            public void Start()
            {
                _owner.ThrowIfDisposed();

                lock (_gate)
                {
                    if (_state != ReceiverState.Created)
                    {
                        return;
                    }

                    _state = ReceiverState.Running;
                }

                PumpAndDispatch();
            }

            public void Shutdown()
            {
                _owner.ThrowIfDisposed();

                lock (_gate)
                {
                    if (IsTerminal || _shutdownReserved)
                    {
                        return;
                    }

                    // Reserve the ending state before leaving the gate so the pump cannot admit a
                    // new receive while the external shutdown is in flight, and so a concurrent
                    // abort that wins the gate can take first-terminal precedence.
                    _shutdownReserved = true;
                }

                // External socket call, outside the gate: a slow/blocked accessor shutdown must not
                // hold the I/O gate or delay abort state admission (C05/P06).
                Exception? error = TryShutdown(_socket, SocketShutdown.Receive);

                bool publish;
                ReceiverCompletionResult result;

                lock (_gate)
                {
                    _shutdownReserved = false;

                    if (IsTerminal)
                    {
                        // A concurrent abort/force already owns terminal state: the late local
                        // shutdown result is discarded (first-terminal-wins).
                        return;
                    }

                    TrySetTerminal(error == null
                        ? new ReceiverCompletionResult(ReceiverCompletionReason.LocalShutdown, null)
                        : new ReceiverCompletionResult(ReceiverCompletionReason.Faulted, error));
                    publish = TryCleanup(out result);
                }

                if (publish)
                {
                    PublishTermination(result);
                }
            }

            internal void Abort()
            {
                bool publish;
                ReceiverCompletionResult result;

                lock (_gate)
                {
                    TrySetTerminal(new ReceiverCompletionResult(ReceiverCompletionReason.Aborted, null));
                    publish = TryCleanup(out result);
                }

                if (publish)
                {
                    PublishTermination(result);
                }
            }

            /// <summary>
            /// Escape-hatch publication used by a bounded shutdown when the native operation will not
            /// complete. The in-flight buffer and event args are abandoned here, not released, because
            /// the runtime may still reference them; any onReceived callback may still be running.
            /// Physical cleanup is <b>not</b> marked complete: a late native completion, an acquisition
            /// return, or a finished dispatch must still reclaim the resources exactly once via
            /// <see cref="TryCleanup"/>.
            /// </summary>
            internal void ForcePublish(Exception? error)
            {
                ReceiverCompletionResult result;

                lock (_gate)
                {
                    if (!_terminalResult.HasValue)
                    {
                        _terminalResult = new ReceiverCompletionResult(ReceiverCompletionReason.Aborted, error);
                    }

                    // Do NOT set _cleanedUp here: TryCleanup keeps the _pending/_pumping/
                    // _activeDispatches guards and remains the sole exactly-once cleanup reservation.
                    result = _terminalResult.Value;
                }

                _owner.RegisterReceiveTermination(result);
                FlushCleanupErrors();
                _completionSource.TrySetResult(result);
                _owner.MarkReceivePublished(result);
            }

            private void OnCompleted(object? sender, SocketAsyncEventArgs args)
            {
                bool terminal;
                bool publish = false;
                ReceiverCompletionResult result = default;

                lock (_gate)
                {
                    _pending = false;
                    terminal = IsTerminal;
                    if (terminal)
                    {
                        publish = TryCleanup(out result);
                    }
                }

                if (terminal)
                {
                    if (publish)
                    {
                        PublishTermination(result);
                    }

                    return;
                }

                PumpAndDispatch();
            }

            private void PumpAndDispatch()
            {
                Exception? error = null;
                bool closed = false;
                bool publish = false;
                ReceiverCompletionResult terminationResult = default;
                IMultiRefByteArray? toDispatch = null;

                lock (_gate)
                {
                    // The pump claims the dispatch slot (if any) under this same lock hold, so an
                    // abort cannot observe zero active dispatches and publish before the callback
                    // starts; the claim also covers the gate-released partial-view owner release.
                    Pump(ref error, ref closed, ref toDispatch, ref publish, ref terminationResult);
                }

                if (publish)
                {
                    PublishTermination(terminationResult);
                    publish = false;
                }

                while (toDispatch != null && error == null && !closed)
                {
                    try
                    {
                        Dispatch(toDispatch, ref error);
                    }
                    finally
                    {
                        lock (_gate)
                        {
                            _activeDispatches--;
                        }
                    }

                    toDispatch = null;

                    if (error != null)
                    {
                        break;
                    }

                    lock (_gate)
                    {
                        Pump(ref error, ref closed, ref toDispatch, ref publish, ref terminationResult);
                    }

                    if (publish)
                    {
                        PublishTermination(terminationResult);
                        publish = false;
                    }
                }

                if (error != null)
                {
                    lock (_gate)
                    {
                        TrySetTerminal(new ReceiverCompletionResult(ReceiverCompletionReason.Faulted, error));
                    }
                }

                // Publish only once no callback is in flight (fully quiesced completion).
                lock (_gate)
                {
                    if (IsTerminal)
                    {
                        publish = TryCleanup(out terminationResult);
                    }
                }

                if (publish)
                {
                    PublishTermination(terminationResult);
                }
            }

            private void Dispatch(IMultiRefByteArray received, ref Exception? error)
            {
                // Ownership of 'received' is transferred to the consumer: _onReceived is responsible
                // for releasing it, including when it throws. The transport never releases it on the
                // consumer's behalf. A thrown callback is reported and faults the receiver.
                try
                {
                    _onReceived(received);
                }
                catch (Exception ex)
                {
                    error = ex;
                    _owner.ReportCallbackError(ex);
                }
            }

            private void Pump(
                ref Exception? error,
                ref bool closed,
                ref IMultiRefByteArray? toDispatch,
                ref bool publishTermination,
                ref ReceiverCompletionResult terminationResult)
            {
                if (_pumping || IsTerminal || _shutdownReserved)
                {
                    return;
                }

                _pumping = true;
                try
                {
                    while (true)
                    {
                        if (IsTerminal || _shutdownReserved)
                        {
                            return;
                        }

                        if (_currentBuffer != null)
                        {
                            var buffer = _currentBuffer;
                            _currentBuffer = null;

                            if (_args.SocketError == SocketError.WouldBlock)
                            {
                                bool exhausted = ++_wouldBlockRetries > MaxWouldBlockRetries;
                                ReleaseBufferAfterClassification(buffer);

                                if (exhausted)
                                {
                                    error = new SocketException((int)SocketError.WouldBlock);
                                    return;
                                }

                                Thread.Yield();
                                continue;
                            }

                            if (_args.SocketError != SocketError.Success)
                            {
                                // Classify the native error BEFORE releasing its buffer: a failing
                                // release must not replace the observed socket fault.
                                error = new SocketException((int)_args.SocketError);
                                ReleaseBufferAfterClassification(buffer);
                                return;
                            }

                            int bytes = _args.BytesTransferred;
                            if (bytes == 0)
                            {
                                TrySetTerminal(new ReceiverCompletionResult(
                                    ReceiverCompletionReason.RemoteEof,
                                    null));
                                ReleaseBufferAfterClassification(buffer);
                                closed = true;
                                return;
                            }

                            IMultiRefByteArray received;
                            if (bytes == buffer.Count)
                            {
                                // The whole rented buffer was filled: hand it over directly, avoiding a
                                // wrapper allocation and the extra ref-count round trip.
                                received = buffer;
                            }
                            else
                            {
                                try
                                {
                                    // Reusable revivable view metadata (P02): no per-receive span
                                    // allocation after warmup within the pooled view capacity.
                                    received = ReusablePartialView.Create(buffer, 0, bytes);
                                }
                                catch
                                {
                                    // The buffer was detached from _currentBuffer above; if handing it to
                                    // the view fails, ownership must still be released exactly once.
                                    ReleaseBufferAfterClassification(buffer);
                                    throw;
                                }

                                ReleaseBufferAfterClassification(buffer);
                            }

                            _wouldBlockRetries = 0;
                            toDispatch = received;

                            // Claim the dispatch slot here, under the same lock hold that produced
                            // the buffer: the partial-view path above released the gate, so a
                            // concurrent terminal transition could otherwise let this pump's
                            // finally reserve cleanup and publish Completion before the pending
                            // dispatch runs. The caller decrements after the callback.
                            _activeDispatches++;
                            return;
                        }

                        IMultiRefByteArray next;
                        try
                        {
                            next = AcquireReceiveBuffer();
                        }
                        catch (Exception ex)
                        {
                            error = ex;
                            return;
                        }

                        // _gate was released while acquiring, so state may have changed.
                        if (IsTerminal || _shutdownReserved)
                        {
                            ReleaseBufferAfterClassification(next);
                            return;
                        }

                        // A zero-capacity buffer must never masquerade as a remote EOF.
                        if (next.Count <= 0)
                        {
                            ReleaseBufferAfterClassification(next);
                            error = new InvalidOperationException("Receive buffer must have positive capacity");
                            return;
                        }

                        _currentBuffer = next;
                        _args.SetBuffer(next.Array, next.Offset, next.Count);

                        if (_socket.ReceiveAsync(_args))
                        {
                            _pending = true;
                            return;
                        }
                    }
                }
                catch (Exception ex)
                {
                    error = ex;
                }
                finally
                {
                    _pumping = false;

                    if (error != null)
                    {
                        TrySetTerminal(new ReceiverCompletionResult(ReceiverCompletionReason.Faulted, error));
                    }

                    if (TryCleanup(out var result))
                    {
                        publishTermination = true;
                        terminationResult = result;
                    }
                }
            }

            private IMultiRefByteArray AcquireReceiveBuffer()
            {
                // Acquire the buffer without holding _gate: the pool may lock internally
                // and we must not delay a completion or a shutdown. No receive is
                // outstanding at this point (_pumping guards reentrancy), so releasing
                // _gate is safe; the caller re-validates the state on re-entry.
                Monitor.Exit(_gate);
                try
                {
                    return _pool.Acquire(_bufferSize);
                }
                finally
                {
                    Monitor.Enter(_gate);
                }
            }

            /// <summary>
            /// Releases a receive buffer whose native outcome has already been classified, with
            /// <see cref="_gate"/> temporarily released so a slow or blocked pool return cannot stall
            /// abort state admission (C05/P06). Must be called while holding <see cref="_gate"/>
            /// exactly once (all call sites are inside <see cref="Pump"/> with the caller's lock
            /// held); the critical section resumes afterwards on the same thread. A release failure is
            /// captured as a cleanup error: it must never replace the classified error.
            /// </summary>
            private void ReleaseBufferAfterClassification(IMultiRefByteArray buffer)
            {
                Monitor.Exit(_gate);
                try
                {
                    buffer.Release();
                }
                catch (Exception ex)
                {
                    CaptureCleanupError(ex);
                }
                finally
                {
                    Monitor.Enter(_gate);
                }
            }

            private void CaptureCleanupError(Exception error)
            {
                lock (_gate)
                {
                    _cleanupErrors.Add(error);
                }
            }

            private void FlushCleanupErrors()
            {
                Exception[] errors;
                lock (_gate)
                {
                    if (_cleanupErrors.Count == 0)
                    {
                        return;
                    }

                    errors = _cleanupErrors.ToArray();
                    _cleanupErrors.Clear();
                }

                foreach (Exception error in errors)
                {
                    _owner.ReportAdditionalError(error);
                }
            }

            private bool TryCleanup(out ReceiverCompletionResult result)
            {
                // Publication (and thus Completion) is deferred until no onReceived callback is running.
                if (_cleanedUp || !IsTerminal || _pending || _pumping || _activeDispatches > 0)
                {
                    result = default;
                    return false;
                }

                _cleanedUp = true;

                // Reserve the terminal result (and the initiating fault's aggregate precedence)
                // before any external cleanup runs. The abort itself is deferred to
                // PublishTermination: executing it here would recursively take directional gates
                // while _gate is held, risking a cross-direction deadlock.
                result = _terminalResult!.Value;
                _abortAfterCleanup = _owner.ReserveReceiveTermination(result);

                // Detach the buffer and args; the external Release/DisposeEventArgs work runs in
                // ExecuteCleanup (via PublishTermination) outside _gate.
                _cleanupBuffer = _currentBuffer;
                _currentBuffer = null;
                _cleanupArgsDetached = true;

                // No native operation references the args here (TryCleanup requires !_pending,
                // !_pumping, and no active dispatch): clear the released array root before the args
                // are eventually disposed (P01). Handed-off consumer views are unaffected.
                _args.SetBuffer(null, 0, 0);

                return true;
            }

            /// <summary>
            /// Performs the reserved exactly-once external cleanup: releases the detached in-flight
            /// buffer and disposes the directional event args. Runs outside every gate; failures are
            /// captured and reported after the initiating terminal result has reserved aggregate
            /// precedence. Called exactly once per cleanup reservation (by
            /// <see cref="PublishTermination"/>).
            /// </summary>
            private void ExecuteCleanup()
            {
                IMultiRefByteArray? buffer;
                bool detachArgs;

                lock (_gate)
                {
                    if (_cleanupExecuted)
                    {
                        return;
                    }

                    _cleanupExecuted = true;
                    buffer = _cleanupBuffer;
                    _cleanupBuffer = null;
                    detachArgs = _cleanupArgsDetached;
                    _cleanupArgsDetached = false;
                }

                if (buffer != null)
                {
                    try
                    {
                        buffer.Release();
                    }
                    catch (Exception ex)
                    {
                        CaptureCleanupError(ex);
                    }
                }

                if (detachArgs)
                {
                    try
                    {
                        _args.Completed -= OnCompleted;
                        _socket.DisposeEventArgs(_args);
                    }
                    catch (Exception ex)
                    {
                        CaptureCleanupError(ex);
                    }
                }
            }

            private void PublishTermination(ReceiverCompletionResult result)
            {
                // Physical cleanup of the reserved resources happens here, outside every gate, before
                // the logical result is published: normal completion must still wait for its reserved
                // cleanup attempts (a blocked cleanup delays publication, not admission).
                ExecuteCleanup();

                bool abort = _abortAfterCleanup;
                _abortAfterCleanup = false;

                FlushCleanupErrors();

                if (abort)
                {
                    _owner.ExecuteAbort();
                }

                _completionSource.TrySetResult(result);
                _owner.MarkReceivePublished(result);
            }

            private bool TrySetTerminal(ReceiverCompletionResult result)
            {
                if (_terminalResult.HasValue)
                {
                    return false;
                }

                _terminalResult = result;
                return true;
            }

            private bool IsTerminal => _terminalResult.HasValue;
        }
    }
}
