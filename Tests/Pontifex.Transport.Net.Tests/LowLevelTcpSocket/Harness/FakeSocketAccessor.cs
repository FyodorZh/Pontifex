using System.Net.Sockets;
using Pontifex.NetSockets.Tcp;

namespace Pontifex.Tests.NetSockets.LowLevelTcpSocketTests.Harness
{
    /// <summary>Kinds of socket-contract misuse the strict accessor can detect.</summary>
    internal enum FakeContractViolation
    {
        /// <summary>A second operation was posted with args that already have one in flight.</summary>
        DoublePost,

        /// <summary>A synchronous (<c>false</c>-returning) result nevertheless raised a completion event.</summary>
        SynchronousResultRaisedEvent,

        /// <summary>A completion event was raised for args that were never posted.</summary>
        CompletionWithoutPost,

        /// <summary>A completion event was raised more than once for a single posted operation.</summary>
        DoubleCompletion,
    }

    /// <summary>
    /// Deterministic test double for <see cref="ISocketAccessor"/>. It hands out
    /// <see cref="TestSocketAsyncEventArgs"/> instances and lets a test script the outcome of
    /// send/receive/shutdown calls instead of touching a real socket.
    /// </summary>
    /// <remarks>
    /// <para>
    /// By default <see cref="SendAsync"/> and <see cref="ReceiveAsync"/> report that the operation
    /// stays pending (return <c>true</c>). Use <see cref="OnSend"/>/<see cref="OnReceive"/> to change
    /// that, and <see cref="LastSendArgs"/>/<see cref="LastReceiveArgs"/> to complete a pending
    /// operation later via <see cref="TestSocketAsyncEventArgs.Complete"/>.
    /// </para>
    /// <para>
    /// <see cref="StrictMode"/> opts in to native socket-contract checks (reject posting an args with
    /// an operation already in flight, flag a synchronous result that raises a completion event, flag
    /// double completions). It is off by default so deliberately malformed defensive tests keep
    /// working; violations observed in strict mode are recorded in <see cref="Violations"/>.
    /// </para>
    /// </remarks>
    internal sealed class FakeSocketAccessor : ISocketAccessor
    {
        private enum ArgsPhase
        {
            Posting,
            Pending,
            Completed,
        }

        private readonly object _sync = new();
        private readonly List<TestSocketAsyncEventArgs> _eventArgs = new();
        private readonly List<SocketShutdown> _shutdownCalls = new();
        private readonly List<TestSocketAsyncEventArgs> _disposedEventArgs = new();
        private readonly Dictionary<TestSocketAsyncEventArgs, ArgsPhase> _phase = new();
        private readonly List<FakeContractViolation> _violations = new();

        private int _sendCount;
        private int _receiveCount;
        private int _disposeCount;
        private int _createEventArgsCount;
        private int _disposeEventArgsCount;

        /// <summary>
        /// Enables native socket-contract checks and violation recording. Defaults to <c>false</c> so
        /// existing tests that intentionally script malformed outcomes are unaffected.
        /// </summary>
        public bool StrictMode { get; set; }

        /// <summary>
        /// Behaviour for <see cref="SendAsync"/>: returns <c>true</c> when the operation remains
        /// pending, or <c>false</c> when it completed synchronously. Defaults to pending.
        /// </summary>
        public Func<TestSocketAsyncEventArgs, bool>? OnSend { get; set; }

        /// <summary>
        /// Behaviour for <see cref="ReceiveAsync"/>: returns <c>true</c> when the operation remains
        /// pending, or <c>false</c> when it completed synchronously. Defaults to pending.
        /// </summary>
        public Func<TestSocketAsyncEventArgs, bool>? OnReceive { get; set; }

        /// <summary>
        /// Behaviour for <see cref="Shutdown"/>. Defaults to recording the call. Throw from here to
        /// simulate platform shutdown failures.
        /// </summary>
        public Action<SocketShutdown>? OnShutdown { get; set; }

        /// <summary>
        /// Optional factory used by <see cref="CreateEventArgs"/> instead of constructing a default
        /// <see cref="TestSocketAsyncEventArgs"/>. Throw from it to script a creation failure.
        /// </summary>
        public Func<TestSocketAsyncEventArgs>? CreateEventArgsFactory { get; set; }

        /// <summary>Invoked with the 1-based ordinal of each <see cref="CreateEventArgs"/> call, before creation.</summary>
        public Action<int>? OnCreateEventArgs { get; set; }

        /// <summary>Invoked at the start of <see cref="Dispose"/>; throw to simulate a disposal failure.</summary>
        public Action? OnDispose { get; set; }

        /// <summary>When set, <see cref="Dispose"/> throws this after recording the call.</summary>
        public Exception? DisposeException { get; set; }

        /// <summary>When set, <see cref="DisposeEventArgs"/> throws this after recording the disposal.</summary>
        public Exception? DisposeEventArgsException { get; set; }

        /// <summary>
        /// Invoked at the start of <see cref="DisposeEventArgs"/> after the call is recorded; throw or
        /// block from here to script a slow/failing args-lifetime adapter.
        /// </summary>
        public Action<TestSocketAsyncEventArgs>? OnDisposeEventArgs { get; set; }

        /// <summary>
        /// Raised at the exact semantic boundary "an asynchronous completion has been raised/observed,
        /// before the transport's <see cref="SocketAsyncEventArgs.Completed"/> callback is dispatched".
        /// A deterministic alternative to polling <see cref="TestSocketAsyncEventArgs.CompletedCount"/>:
        /// the transport callback may block on an I/O gate held by the posting thread, so waiting for
        /// the callback instead of this hook can deadlock. Never raised for a synchronous result
        /// (<see cref="TestSocketAsyncEventArgs.SetSynchronousResult"/>).
        /// </summary>
        internal event Action<TestSocketAsyncEventArgs>? CompletionRaised;

        /// <summary>All <see cref="SocketAsyncEventArgs"/> instances handed to the socket under test.</summary>
        public TestSocketAsyncEventArgs[] EventArgs
        {
            get { lock (_sync) { return _eventArgs.ToArray(); } }
        }

        /// <summary>The shutdown directions requested so far, in call order.</summary>
        public SocketShutdown[] ShutdownCalls
        {
            get { lock (_sync) { return _shutdownCalls.ToArray(); } }
        }

        /// <summary>Contract violations observed while <see cref="StrictMode"/> was enabled, in call order.</summary>
        public FakeContractViolation[] Violations
        {
            get { lock (_sync) { return _violations.ToArray(); } }
        }

        /// <summary>Operations that were posted with <c>true</c> and have not completed yet.</summary>
        public TestSocketAsyncEventArgs[] PendingArgs
        {
            get
            {
                lock (_sync)
                {
                    return _phase.Where(pair => pair.Value == ArgsPhase.Pending)
                        .Select(pair => pair.Key)
                        .ToArray();
                }
            }
        }

        /// <summary>Number of operations that were posted with <c>true</c> and have not completed yet.</summary>
        public int PendingCount
        {
            get { lock (_sync) { return _phase.Count(pair => pair.Value == ArgsPhase.Pending); } }
        }

        public int SendCount => Volatile.Read(ref _sendCount);

        public int ReceiveCount => Volatile.Read(ref _receiveCount);

        public int DisposeCount => Volatile.Read(ref _disposeCount);

        public int CreateEventArgsCount => Volatile.Read(ref _createEventArgsCount);

        /// <summary>Number of <see cref="DisposeEventArgs"/> calls observed (args-lifetime seam).</summary>
        public int DisposeEventArgsCount => Volatile.Read(ref _disposeEventArgsCount);

        /// <summary>The args instances passed to <see cref="DisposeEventArgs"/>, in call order.</summary>
        public TestSocketAsyncEventArgs[] DisposedEventArgs
        {
            get { lock (_sync) { return _disposedEventArgs.ToArray(); } }
        }

        public bool IsDisposed => DisposeCount > 0;

        public TestSocketAsyncEventArgs? LastSendArgs { get; private set; }

        public TestSocketAsyncEventArgs? LastReceiveArgs { get; private set; }

        public SocketShutdown? LastShutdown { get; private set; }

        /// <summary>True when <paramref name="args"/> was posted with <c>true</c> and has not completed yet.</summary>
        public bool IsPending(TestSocketAsyncEventArgs args)
        {
            lock (_sync)
            {
                return _phase.TryGetValue(args, out ArgsPhase phase) && phase == ArgsPhase.Pending;
            }
        }

        public SocketAsyncEventArgs CreateEventArgs()
        {
            int ordinal = Interlocked.Increment(ref _createEventArgsCount);
            OnCreateEventArgs?.Invoke(ordinal);

            TestSocketAsyncEventArgs args = CreateEventArgsFactory?.Invoke()
                                            ?? new TestSocketAsyncEventArgs();
            args.CompletionRaising += OnCompletionRaising;

            lock (_sync)
            {
                _eventArgs.Add(args);
            }

            return args;
        }

        public bool SendAsync(SocketAsyncEventArgs e)
        {
            Interlocked.Increment(ref _sendCount);
            var args = (TestSocketAsyncEventArgs)e;
            LastSendArgs = args;
            return Post(args, OnSend);
        }

        public bool ReceiveAsync(SocketAsyncEventArgs e)
        {
            Interlocked.Increment(ref _receiveCount);
            var args = (TestSocketAsyncEventArgs)e;
            LastReceiveArgs = args;
            return Post(args, OnReceive);
        }

        public void Shutdown(SocketShutdown how)
        {
            lock (_sync)
            {
                _shutdownCalls.Add(how);
            }

            LastShutdown = how;
            OnShutdown?.Invoke(how);
        }

        public void DisposeEventArgs(SocketAsyncEventArgs e)
        {
            Interlocked.Increment(ref _disposeEventArgsCount);
            var args = (TestSocketAsyncEventArgs)e;
            lock (_sync)
            {
                _disposedEventArgs.Add(args);
            }

            OnDisposeEventArgs?.Invoke(args);

            if (DisposeEventArgsException != null)
            {
                throw DisposeEventArgsException;
            }

            e.Dispose();
        }

        public void Dispose()
        {
            Interlocked.Increment(ref _disposeCount);
            OnDispose?.Invoke();

            if (DisposeException != null)
            {
                throw DisposeException;
            }
        }

        /// <summary>
        /// Completes every operation currently posted as pending with the given error, then verifies
        /// that no fake operation remains outstanding. This is the explicit bounded-teardown helper;
        /// disposal itself never fabricates native completions.
        /// </summary>
        /// <returns>The number of operations completed by this call.</returns>
        public int CompletePendingOperations(SocketError socketError = SocketError.OperationAborted)
        {
            TestSocketAsyncEventArgs[] pending;
            lock (_sync)
            {
                pending = _phase.Where(pair => pair.Value == ArgsPhase.Pending)
                    .Select(pair => pair.Key)
                    .ToArray();
            }

            foreach (TestSocketAsyncEventArgs args in pending)
            {
                args.Complete(0, socketError);
            }

            lock (_sync)
            {
                TestSocketAsyncEventArgs[] remaining = _phase.Where(pair => pair.Value == ArgsPhase.Pending)
                    .Select(pair => pair.Key)
                    .ToArray();
                if (remaining.Length != 0)
                {
                    throw new InvalidOperationException(
                        $"{remaining.Length} fake operation(s) are still pending after bounded teardown.");
                }
            }

            return pending.Length;
        }

        private bool Post(TestSocketAsyncEventArgs args, Func<TestSocketAsyncEventArgs, bool>? script)
        {
            lock (_sync)
            {
                if (_phase.TryGetValue(args, out ArgsPhase existing)
                    && (existing == ArgsPhase.Posting || existing == ArgsPhase.Pending))
                {
                    RecordViolationLocked(FakeContractViolation.DoublePost);
                    if (StrictMode)
                    {
                        throw new InvalidOperationException(
                            "SocketAsyncEventArgs already has an operation in flight; posting it again is not allowed.");
                    }
                }

                _phase[args] = ArgsPhase.Posting;
            }

            bool pending;
            try
            {
                pending = script?.Invoke(args) ?? true;
            }
            catch
            {
                lock (_sync)
                {
                    _phase[args] = ArgsPhase.Completed;
                }

                throw;
            }

            lock (_sync)
            {
                // A completion raised while the post was in flight (possibly on another thread) must
                // not be re-marked pending when the posting call later returns true.
                bool completedWhilePosting = _phase.TryGetValue(args, out ArgsPhase phase)
                                             && phase == ArgsPhase.Completed;
                if (completedWhilePosting)
                {
                    if (!pending)
                    {
                        RecordViolationLocked(FakeContractViolation.SynchronousResultRaisedEvent);
                    }

                    _phase[args] = ArgsPhase.Completed;
                }
                else
                {
                    _phase[args] = pending ? ArgsPhase.Pending : ArgsPhase.Completed;
                }
            }

            return pending;
        }

        private void OnCompletionRaising(TestSocketAsyncEventArgs args)
        {
            lock (_sync)
            {
                if (_phase.TryGetValue(args, out ArgsPhase phase))
                {
                    if (phase == ArgsPhase.Posting || phase == ArgsPhase.Pending)
                    {
                        // Completing during the posting call is legal for an asynchronous operation;
                        // Post() decides after the call returns whether that was malformed.
                        _phase[args] = ArgsPhase.Completed;
                    }
                    else if (phase == ArgsPhase.Completed)
                    {
                        RecordViolationLocked(FakeContractViolation.DoubleCompletion);
                    }
                    else
                    {
                        RecordViolationLocked(FakeContractViolation.CompletionWithoutPost);
                        _phase[args] = ArgsPhase.Completed;
                    }
                }
                else
                {
                    RecordViolationLocked(FakeContractViolation.CompletionWithoutPost);
                    _phase[args] = ArgsPhase.Completed;
                }
            }

            // Signal after the phase transition but BEFORE OnCompleted(this) dispatches to transport
            // handlers: a handler may block on the sender's I/O gate held by the posting thread, so
            // handshaking on the callback itself could deadlock.
            CompletionRaised?.Invoke(args);
        }

        private void RecordViolationLocked(FakeContractViolation violation)
        {
            if (!StrictMode)
            {
                return;
            }

            _violations.Add(violation);
        }
    }
}
