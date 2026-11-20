using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Actuarius.Collections;
using Actuarius.Memory;
using Pontifex.NetSockets;
using Pontifex.NetSockets.Tcp;
using Pontifex.StopReasons;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Raw.Reliable.Tcp
{
    /// <summary>
    /// Reliable TCP server endpoint built on <see cref="LowLevelTcpSocket"/>.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Thread safety</b>: Lifecycle hooks run on the base serial action queue; the
    /// outbound source has a single writer (that queue), while socket callbacks may run on socket or
    /// thread-pool threads.</description></item>
    /// <item><description><b>Transport handshake</b>: the accepted socket is already connected and its
    /// handshake request has been read, validated, and answered by <see cref="BeginAccept"/>. The endpoint
    /// sends the prebuilt response from <c>OnConnected</c> before the handler observes the
    /// connection.</description></item>
    /// <item><description><b>Other notes</b>: The endpoint takes ownership of an already-connected socket that the
    /// carrier accepted. It is created non-started; the caller must call Start or Stop. There is no
    /// <c>ToString</c> override: the transport owns the <c>tcp-server[...]</c> label.</description></item>
    /// </list>
    /// </remarks>
    internal sealed class RawReliableTcpServerEndpoint : RawReliableHandshakeServerEndpoint
    {
        private const int DefaultReceiveBufferSize = 16 * 1024;

        private readonly TimeSpan _disconnectTimeout;
        private readonly RemoteHalfClosePolicy _remoteHalfClosePolicy;
        private readonly int _receiveBufferSize;
        private readonly int _messagePartMaxSize;
        private readonly TimeSpan _gracefulShutdownDeadline;
        private readonly int _sendSourceInitialCapacity;

        // Guards the "am I allowed to complete the connect" decision against FailHard.
        private readonly object _lifecycleGate = new();
        // Serializes receive-compositor use against its disposal after a forced/bounded completion.
        private readonly object _receiveGate = new();

        private SingleReaderWriterConcurrentQueue<IMultiRefByteArray>? _source;
        private UnionDataListCompositor? _receiveCompositor;
        private LowLevelTcpSocket? _lowLevel;
        private Socket? _socket;

        private int _teardownStarted;
        private int _finishDisconnectIssued;
        private volatile bool _connectSucceeded;
        private volatile bool _connectFailed;
        private volatile bool _failedHard;
        private bool _receiveDisposed;

        /// <summary>
        /// Initializes a reliable TCP server endpoint from an already-accepted socket.
        /// </summary>
        /// <param name="transportName">The name of the transport that owns the endpoint.</param>
        /// <param name="remoteEndPoint">The remote endpoint of the accepted client.</param>
        /// <param name="socket">The accepted, connected socket. Ownership transfers to the endpoint.</param>
        /// <param name="handler">The session handler that receives the connection lifecycle notifications and
        /// inbound messages.</param>
        /// <param name="pendingHandshakeResponse">The prebuilt handshake response without its frame type.
        /// Ownership transfers to the endpoint.</param>
        /// <param name="messageMaxByteSize">The inclusive maximum message size, in bytes, admitted by the
        /// endpoint; must be at least 1.</param>
        /// <param name="bufferCapacityBytes">The byte budget for messages accepted by the endpoint but not
        /// yet transmitted; must be at least 1.</param>
        /// <param name="memory">The memory rental system used to rent buffers.</param>
        /// <param name="log">The logging system used by the endpoint.</param>
        /// <param name="disconnectTimeout">The maximum time without an inbound message before the endpoint
        /// is stopped as timed out; must be positive.</param>
        /// <param name="remoteHalfClosePolicy">The action to take when the remote peer half-closes.</param>
        /// <param name="receiveBufferSize">The capacity of every pooled receive buffer; must be
        /// positive.</param>
        /// <param name="messagePartMaxSize">The maximum size of one encoded frame part; must be
        /// positive.</param>
        /// <param name="gracefulShutdownDeadline">The deadline for a graceful shutdown; defaults to ten
        /// seconds. <see cref="Timeout.InfiniteTimeSpan"/> waits indefinitely.</param>
        /// <param name="sendSourceInitialCapacity">The initial capacity of the outbound source; must be at
        /// least 1.</param>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Preconditions/Postconditions</b>: The socket must be connected and is owned by
        /// the endpoint from this point on. The endpoint starts in the constructed stage.</description></item>
        /// <item><description><b>Other notes</b>: The constructor is internal because the reliable
        /// server transport creates the endpoint directly from its asynchronous handshake completion.</description></item>
        /// </list>
        /// </remarks>
        internal RawReliableTcpServerEndpoint(
            string transportName,
            IEndPoint remoteEndPoint,
            Socket socket,
            IRawReliableServerHandler handler,
            UnionDataList pendingHandshakeResponse,
            int messageMaxByteSize,
            long bufferCapacityBytes,
            IMemoryRental memory,
            ILogger log,
            TimeSpan disconnectTimeout,
            RemoteHalfClosePolicy remoteHalfClosePolicy,
            int receiveBufferSize,
            int messagePartMaxSize,
            TimeSpan gracefulShutdownDeadline,
            int sendSourceInitialCapacity)
            : base(
                transportName,
                remoteEndPoint,
                handler,
                messageMaxByteSize,
                bufferCapacityBytes,
                memory,
                log,
                disconnectTimeout)
        {
            if (socket == null)
            {
                throw new ArgumentNullException(nameof(socket));
            }

            _disconnectTimeout = disconnectTimeout;
            _remoteHalfClosePolicy = remoteHalfClosePolicy;
            _receiveBufferSize = receiveBufferSize;
            _messagePartMaxSize = messagePartMaxSize;
            _gracefulShutdownDeadline = gracefulShutdownDeadline;
            _sendSourceInitialCapacity = sendSourceInitialCapacity;

            _socket = socket;
            SetPendingHandshakeResponse(pendingHandshakeResponse);
        }

        /// <summary>
        /// Gets the current time used by the idle-session watchdog.
        /// </summary>
        protected override DateTime Now => DateTime.UtcNow;

        /// <summary>
        /// Configures the accepted socket and creates the low-level transport. Runs synchronously on the
        /// serial action queue and completes the start without waiting for network I/O.
        /// </summary>
        protected override void BeginConnect()
        {
            Socket? socket = _socket;
            ISocketAccessor? accessor = null;
            LowLevelTcpSocket? lowLevel = null;

            try
            {
                if (socket == null)
                {
                    throw new InvalidOperationException("Server endpoint has no accepted socket");
                }

                socket.ReceiveTimeout = (int)_disconnectTimeout.TotalMilliseconds;
                socket.SendTimeout = (int)_disconnectTimeout.TotalMilliseconds;
                socket.NoDelay = true;

                _source = new SingleReaderWriterConcurrentQueue<IMultiRefByteArray>(_sendSourceInitialCapacity);
                _receiveCompositor = new UnionDataListCompositor(
                    OnFramedMessage, Memory.CollectablePool, Memory.ByteArraysPool, MessageMaxByteSize);

                accessor = new SystemSocket(socket);
                lowLevel = new LowLevelTcpSocket(
                    accessor,
                    _source,
                    OnReceived,
                    Memory.ByteArraysPool,
                    _remoteHalfClosePolicy,
                    OnCallbackError,
                    _receiveBufferSize,
                    _gracefulShutdownDeadline);

                _lowLevel = lowLevel;
                _socket = null;

                _ = lowLevel.Completion.ContinueWith(
                    t =>
                    {
                        if (t.Status == TaskStatus.RanToCompletion)
                        {
                            OnSocketCompleted(t.Result);
                        }
                    },
                    TaskScheduler.Default);

                _connectSucceeded = true;
                FinishConnect(BeginAbortLowLevel);
            }
            catch (Exception ex)
            {
                _connectSucceeded = false;
                _connectFailed = true;
                if (lowLevel != null)
                {
                    TryDispose(lowLevel);
                }
                else if (accessor != null)
                {
                    TryDispose(accessor);
                }
                else
                {
                    TryDispose(socket);
                }

                _socket = null;
                _lowLevel = null;
                Stop(new ExceptionFail(TransportName, ex, "Failed to open server endpoint"), false);
                FinishConnect(BeginAbortLowLevel);
            }
        }

        /// <summary>
        /// Sends the prebuilt handshake response, notifies the handler of the connection, and starts the
        /// inherited idle-session watchdog, then starts receiving.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Called on the serial action queue.</description></item>
        /// <item><description><b>Other notes</b>: The receiver starts only when the endpoint is still running
        /// after the base handshake completion. A missing or unsendable response, a handler exception, or a
        /// handler-initiated disconnect stops the endpoint synchronously, so the receiver must not be started
        /// for it.</description></item>
        /// </list>
        /// </remarks>
        protected override void OnConnected()
        {
            if (!_connectSucceeded)
            {
                return;
            }

            var lowLevel = _lowLevel;
            if (lowLevel == null)
            {
                return;
            }

            base.OnConnected();

            if (!IsRunning)
            {
                return;
            }

            try
            {
                lowLevel.Receiver.Start();
            }
            catch (Exception ex)
            {
                Stop(new ExceptionFail(TransportName, ex, "Failed to start receiver"), false);
            }
        }

        /// <summary>
        /// Frames and enqueues one outbound message; runs on the single-writer action queue.
        /// </summary>
        protected override void DoSendRaw(UnionDataList bufferToSend)
        {
            using var disposer = bufferToSend.AsDisposable();

            var lowLevel = _lowLevel;
            if (lowLevel == null)
            {
                Stop(new TextFail(TransportName, "No low-level socket"), false);
                return;
            }

            if (!UnionDataListCompositor.Encode(bufferToSend, Memory.ByteArraysPool, _messagePartMaxSize, _source!))
            {
                Stop(new TextFail(TransportName, "Failed to encode packet"), false);
                return;
            }

            try
            {
                lowLevel.Sender.Pump();
            }
            catch (ObjectDisposedException)
            {
                // The endpoint is already tearing down.
            }
            catch (Exception ex)
            {
                Stop(new ExceptionFail(TransportName, ex), false);
            }
        }

        /// <summary>
        /// Notifies the handler of the logical disconnection and starts the socket teardown in the background.
        /// </summary>
        protected override void BeginDisconnect(StopReason reason, bool tryBeGentle)
        {
            base.BeginDisconnect(reason, tryBeGentle);

            if (_lowLevel == null)
            {
                // Failed-connect path: no low-level transport/receiver was ever started, but the
                // receive compositor and source were allocated in BeginConnect.
                DisposeReceiveCompositor();
                DrainSource();
                FinishDisconnectOnce();
                return;
            }

            if (Interlocked.CompareExchange(ref _teardownStarted, 1, 0) == 0)
            {
                _ = Task.Run(() => RunTeardownAsync(tryBeGentle));
            }
        }

        /// <summary>
        /// Releases the low-level transport immediately during a hard failure.
        /// </summary>
        protected override void FailHard(StopReason reason)
        {
            lock (_lifecycleGate)
            {
                _failedHard = true;
            }

            try
            {
                base.FailHard(reason);
            }
            finally
            {
                if (Interlocked.CompareExchange(ref _teardownStarted, 1, 0) == 0)
                {
                    _ = Task.Run(() => AbortLowLevelAsync());
                }
                else
                {
                    // A teardown already owns the flow (possibly an unbounded graceful wait). Force
                    // the low level terminal so a hard failure can never be stuck behind it.
                    var lowLevel = _lowLevel;
                    if (lowLevel != null)
                    {
                        _ = lowLevel.DisposeAsync();
                    }
                }
            }
        }

        private void OnReceived(IMultiRefByteArray buffer)
        {
            try
            {
                lock (_receiveGate)
                {
                    if (_receiveDisposed)
                    {
                        return;
                    }

                    _receiveCompositor!.PushData(buffer.Array, buffer.Offset, buffer.Count);
                }
            }
            finally
            {
                buffer.Release();
            }
        }

        private void OnFramedMessage(UnionDataList? message)
        {
            if (message == null)
            {
                Stop(new TextFail(TransportName, "Failed to decompose packet from stream"), false);
                return;
            }

            ProcessInboundMessage(message);
        }

        private void OnCallbackError(Exception ex)
        {
            Log.wtf(ex);

            if (Volatile.Read(ref _teardownStarted) != 0 || _failedHard || _connectFailed)
            {
                return;
            }

            Stop(new ExceptionFail(TransportName, ex), false);
        }

        private void OnSocketCompleted(SocketCompletionResult result)
        {
            try
            {
                if (Volatile.Read(ref _teardownStarted) != 0 || _failedHard || _connectFailed)
                {
                    return;
                }

                // Disposed is always a local teardown that this endpoint initiated.
                if (result.Reason == SocketCompletionReason.Disposed)
                {
                    return;
                }

                if (result.Reason == SocketCompletionReason.Faulted || result.Error != null)
                {
                    Stop(new ExceptionFail(TransportName, result.Error ?? new Exception("Socket faulted")), false);
                }
                else
                {
                    Stop(new UnknownRemoteIntention(TransportName), false);
                }
            }
            catch (Exception ex)
            {
                Log.wtf(ex);
            }
        }

        private async Task RunTeardownAsync(bool gentle)
        {
            try
            {
                var lowLevel = _lowLevel;
                if (lowLevel != null)
                {
                    bool graceful = gentle && !_failedHard;

                    if (graceful && !lowLevel.Completion.IsCompleted)
                    {
                        bool encoded = true;
                        try
                        {
                            var message = Memory.CollectablePool.Acquire<UnionDataList>();
                            using var disposer = message.AsDisposable();
                            message.PutFirst((byte)MessageType.DisconnectMessage);
                            encoded = UnionDataListCompositor.Encode(
                                message, Memory.ByteArraysPool, _messagePartMaxSize, _source!);
                        }
                        catch (Exception ex)
                        {
                            Log.wtf(ex);
                            encoded = false;
                        }

                        // A partially encoded frame must never be pumped; escalate to an immediate
                        // shutdown, which stops sending and leaves the leftovers for DrainSource.
                        if (!encoded)
                        {
                            Log.wtf(new Exception("Failed to encode Disconnect message"));
                            graceful = false;
                        }
                    }

                    if (graceful)
                    {
                        if (!lowLevel.Completion.IsCompleted)
                        {
                            try
                            {
                                lowLevel.Sender.Shutdown(true, _gracefulShutdownDeadline);
                            }
                            catch (ObjectDisposedException)
                            {
                            }
                            catch (Exception ex)
                            {
                                Log.wtf(ex);
                            }

                            bool done = await WaitAsync(lowLevel.Completion, _gracefulShutdownDeadline)
                                .ConfigureAwait(false);
                            if (!done || _failedHard)
                            {
                                TryShutdownReceiver(lowLevel);
                                await DisposeLowLevelAsync(lowLevel, TimeSpan.Zero).ConfigureAwait(false);
                            }
                        }
                    }
                    else
                    {
                        try
                        {
                            lowLevel.Sender.Shutdown(false);
                        }
                        catch (ObjectDisposedException)
                        {
                        }
                        catch (Exception ex)
                        {
                            Log.wtf(ex);
                        }

                        TryShutdownReceiver(lowLevel);
                        await DisposeLowLevelAsync(lowLevel, _gracefulShutdownDeadline).ConfigureAwait(false);
                    }

                    await AwaitQuietly(lowLevel.Completion).ConfigureAwait(false);
                    await AwaitQuietly(lowLevel.Sender.SourceQuiesced).ConfigureAwait(false);
                }

                DrainSource();
            }
            catch (Exception ex)
            {
                Log.wtf(ex);

                // Do not drain concurrently with the sender's reader: bound the abort and wait for
                // sender quiescence before reclaiming any queued buffers.
                var lowLevel = _lowLevel;
                if (lowLevel != null)
                {
                    await DisposeLowLevelAsync(lowLevel, _gracefulShutdownDeadline).ConfigureAwait(false);
                    await AwaitQuietly(lowLevel.Completion).ConfigureAwait(false);
                    await AwaitQuietly(lowLevel.Sender.SourceQuiesced).ConfigureAwait(false);
                }

                DrainSource();
            }
            finally
            {
                DisposeReceiveCompositor();
                FinishDisconnectOnce();
            }
        }

        private void FinishDisconnectOnce()
        {
            if (_failedHard)
            {
                return;
            }

            if (Interlocked.Exchange(ref _finishDisconnectIssued, 1) == 0)
            {
                FinishDisconnect();
            }
        }

        private void BeginAbortLowLevel()
        {
            if (Interlocked.CompareExchange(ref _teardownStarted, 1, 0) == 0)
            {
                _ = Task.Run(() => AbortLowLevelAsync());
            }
        }

        private async Task AbortLowLevelAsync()
        {
            try
            {
                var lowLevel = _lowLevel;
                if (lowLevel != null)
                {
                    await DisposeLowLevelAsync(lowLevel, _gracefulShutdownDeadline).ConfigureAwait(false);
                    await AwaitQuietly(lowLevel.Completion).ConfigureAwait(false);
                    await AwaitQuietly(lowLevel.Sender.SourceQuiesced).ConfigureAwait(false);
                }

                DrainSource();
            }
            finally
            {
                DisposeReceiveCompositor();
            }
        }

        private void DisposeReceiveCompositor()
        {
            lock (_receiveGate)
            {
                if (_receiveDisposed)
                {
                    return;
                }

                _receiveDisposed = true;
                TryDispose(_receiveCompositor);
            }
        }

        private void DrainSource()
        {
            var source = _source;
            if (source == null)
            {
                return;
            }

            while (true)
            {
                IMultiRefByteArray? buffer = null;
                try
                {
                    if (!source.TryPop(out buffer))
                    {
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Log.wtf(ex);
                    return;
                }

                try
                {
                    buffer!.Release();
                }
                catch (Exception ex)
                {
                    Log.wtf(ex);
                }
            }
        }

        private static async Task<bool> WaitAsync(Task task, TimeSpan timeout)
        {
            if (task.IsCompleted)
            {
                return true;
            }

            if (timeout == Timeout.InfiniteTimeSpan)
            {
                await task.ConfigureAwait(false);
                return true;
            }

            using var cts = new CancellationTokenSource();
            var delay = Task.Delay(timeout, cts.Token);
            var completed = await Task.WhenAny(task, delay).ConfigureAwait(false);
            if (completed == task)
            {
                cts.Cancel();
                return true;
            }

            return false;
        }

        #region Allocation-free best-effort helpers

        // Helpers take concrete operands instead of delegates, so wrapping a call does not allocate a
        // closure or a delegate. Only the exception path (logging) may allocate.

        private void TryShutdownReceiver(LowLevelTcpSocket lowLevel)
        {
            try
            {
                lowLevel.Receiver.Shutdown();
            }
            catch (ObjectDisposedException)
            {
            }
            catch (Exception ex)
            {
                Log.wtf(ex);
            }
        }

        private void TryDispose(IDisposable? disposable)
        {
            if (disposable == null)
            {
                return;
            }

            try
            {
                disposable.Dispose();
            }
            catch (Exception ex)
            {
                Log.wtf(ex);
            }
        }

        private async Task AwaitQuietly(Task task)
        {
            try
            {
                await task.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.wtf(ex);
            }
        }

        private async Task DisposeLowLevelAsync(LowLevelTcpSocket lowLevel, TimeSpan deadline)
        {
            try
            {
                await lowLevel.DisposeAsync(deadline).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.wtf(ex);
            }
        }

        #endregion

        /// <summary>
        /// The terminal status of a reliable TCP server-side handshake attempt.
        /// </summary>
        internal enum TcpHandshakeStatus
        {
            /// <summary>
            /// The handshake succeeded and a non-started endpoint was created.
            /// </summary>
            Accepted,

            /// <summary>
            /// The handler factory or the transport rejected the session.
            /// </summary>
            Rejected,

            /// <summary>
            /// The peer closed the connection during the handshake.
            /// </summary>
            Eof,

            /// <summary>
            /// The overall handshake deadline elapsed.
            /// </summary>
            Timeout,

            /// <summary>
            /// The advertised handshake length exceeded the configured maximum; the body was never
            /// allocated.
            /// </summary>
            MessageTooBig,

            /// <summary>
            /// The advertised length, the payload, or the first frame type violated the protocol.
            /// </summary>
            ProtocolError,

            /// <summary>
            /// An exception occurred during the handshake.
            /// </summary>
            Faulted
        }

        /// <summary>
        /// The result of a reliable TCP server-side handshake attempt.
        /// </summary>
        internal readonly struct TcpHandshakeResult
        {
            public TcpHandshakeStatus Status { get; }

            public RawReliableTcpServerEndpoint? Endpoint { get; }

            public IRawReliableServerHandler? Handler { get; }

            public StopReason? Reason { get; }

            public Exception? Error { get; }

            private TcpHandshakeResult(TcpHandshakeStatus status, RawReliableTcpServerEndpoint? endpoint,
                IRawReliableServerHandler? handler, StopReason? reason, Exception? error)
            {
                Status = status;
                Endpoint = endpoint;
                Handler = handler;
                Reason = reason;
                Error = error;
            }

            public static TcpHandshakeResult Accepted(
                RawReliableTcpServerEndpoint endpoint, IRawReliableServerHandler handler)
            {
                return new TcpHandshakeResult(TcpHandshakeStatus.Accepted, endpoint, handler, null, null);
            }

            public static TcpHandshakeResult Rejected(StopReason reason)
            {
                return new TcpHandshakeResult(TcpHandshakeStatus.Rejected, null, null, reason, null);
            }

            public static TcpHandshakeResult EndOfStream(string transportName)
            {
                return new TcpHandshakeResult(TcpHandshakeStatus.Eof, null, null,
                    new UnknownRemoteIntention(transportName), null);
            }

            public static TcpHandshakeResult TimedOut(string transportName)
            {
                return new TcpHandshakeResult(TcpHandshakeStatus.Timeout, null, null,
                    new TimeOut(transportName), null);
            }

            public static TcpHandshakeResult PayloadTooBig(string transportName, int max)
            {
                return new TcpHandshakeResult(TcpHandshakeStatus.MessageTooBig, null, null,
                    new TextFail(transportName, "Handshake exceeds HandshakeMaxByteSize (max {0})", max), null);
            }

            public static TcpHandshakeResult ProtocolFailure(string transportName, string text)
            {
                return new TcpHandshakeResult(TcpHandshakeStatus.ProtocolError, null, null,
                    new TextFail(transportName, text), null);
            }

            public static TcpHandshakeResult Faulted(
                string transportName, Exception error, IRawReliableServerHandler? handler = null)
            {
                return new TcpHandshakeResult(TcpHandshakeStatus.Faulted, null, handler,
                    new ExceptionFail(transportName, error, "Transport handshake failed"), error);
            }
        }

        /// <summary>
        /// Reads the client's transport handshake from an accepted socket and creates a non-started endpoint
        /// when the handler factory admits the session.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Preconditions/Postconditions</b>: The handshake length prefix is validated
        /// before any body buffer is allocated, so an oversized advertisement cannot force a large allocation.
        /// A rejected handshake writes a reject frame, waits the configured reject linger, and closes the
        /// socket.</description></item>
        /// <item><description><b>Thread safety</b>: The completion callback is serialized by an internal guard
        /// and is invoked exactly once.</description></item>
        /// </list>
        /// </remarks>
        internal static void BeginAccept(
            string transportName,
            Socket socket,
            Func<IEndPoint, UnionDataList, HandshakeOutcome> handlerFactory,
            string handshakeName,
            int handshakeMaxByteSize,
            int messageMaxByteSize,
            long bufferCapacityBytes,
            IMemoryRental memory,
            ILogger log,
            TimeSpan disconnectTimeout,
            TimeSpan rejectLinger,
            Action<TcpHandshakeResult> onCompleted,
            TimeSpan? handshakeTimeout = null,
            RemoteHalfClosePolicy remoteHalfClosePolicy = RemoteHalfClosePolicy.GracefulSendShutdown,
            int receiveBufferSize = DefaultReceiveBufferSize,
            int messagePartMaxSize = DefaultReceiveBufferSize,
            TimeSpan? gracefulShutdownDeadline = null,
            int sendSourceInitialCapacity = 10)
        {
            if (socket == null)
            {
                throw new ArgumentNullException(nameof(socket));
            }

            if (handlerFactory == null)
            {
                throw new ArgumentNullException(nameof(handlerFactory));
            }

            if (handshakeName == null)
            {
                throw new ArgumentNullException(nameof(handshakeName));
            }

            if (memory == null)
            {
                throw new ArgumentNullException(nameof(memory));
            }

            if (log == null)
            {
                throw new ArgumentNullException(nameof(log));
            }

            if (onCompleted == null)
            {
                throw new ArgumentNullException(nameof(onCompleted));
            }

            if (handshakeMaxByteSize < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(handshakeMaxByteSize));
            }

            if (messageMaxByteSize < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(messageMaxByteSize));
            }

            if (bufferCapacityBytes < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(bufferCapacityBytes));
            }

            if (disconnectTimeout <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(disconnectTimeout));
            }

            if (rejectLinger < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(rejectLinger));
            }

            if (!Enum.IsDefined(typeof(RemoteHalfClosePolicy), remoteHalfClosePolicy))
            {
                throw new ArgumentOutOfRangeException(nameof(remoteHalfClosePolicy));
            }

            if (receiveBufferSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(receiveBufferSize));
            }

            if (messagePartMaxSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(messagePartMaxSize));
            }

            if (sendSourceInitialCapacity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(sendSourceInitialCapacity));
            }

            var handshakeDeadline = handshakeTimeout ?? TimeSpan.FromSeconds(10);
            if (handshakeDeadline != Timeout.InfiniteTimeSpan && handshakeDeadline <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(handshakeTimeout));
            }

            var shutdownDeadline = gracefulShutdownDeadline ?? TimeSpan.FromSeconds(10);
            if (shutdownDeadline != Timeout.InfiniteTimeSpan &&
                (shutdownDeadline < TimeSpan.Zero || shutdownDeadline > LowLevelTcpSocket.MaxFiniteDeadline))
            {
                throw new ArgumentOutOfRangeException(nameof(gracefulShutdownDeadline));
            }

            var handshake = new AcceptHandshake(
                transportName,
                socket,
                handlerFactory,
                handshakeName,
                handshakeMaxByteSize,
                messageMaxByteSize,
                bufferCapacityBytes,
                memory,
                log,
                disconnectTimeout,
                rejectLinger,
                onCompleted,
                handshakeDeadline,
                remoteHalfClosePolicy,
                receiveBufferSize,
                messagePartMaxSize,
                shutdownDeadline,
                sendSourceInitialCapacity,
                (byte)MessageType.HandshakeRequestMessage,
                (byte)MessageType.HandshakeRejectMessage);

            handshake.Start();
        }

        /// <summary>
        /// Callback-driven transport handshake. Reads the four-byte length prefix, validates the advertised
        /// body length against the configured maximum before allocating, reads and deserializes the body, and
        /// invokes the handler factory. Uses APM and a single timer; no Task or async.
        /// </summary>
        private sealed class AcceptHandshake
        {
            private const int PrefixSize = 4;

            private readonly string _transportName;
            private readonly Socket _socket;
            private readonly Func<IEndPoint, UnionDataList, HandshakeOutcome> _handlerFactory;
            private readonly string _handshakeName;
            private readonly int _handshakeMaxByteSize;
            private readonly int _messageMaxByteSize;
            private readonly long _bufferCapacityBytes;
            private readonly IMemoryRental _memory;
            private readonly ILogger _log;
            private readonly TimeSpan _disconnectTimeout;
            private readonly TimeSpan _rejectLinger;
            private readonly Action<TcpHandshakeResult> _onCompleted;
            private readonly TimeSpan _handshakeTimeout;
            private readonly RemoteHalfClosePolicy _remoteHalfClosePolicy;
            private readonly int _receiveBufferSize;
            private readonly int _messagePartMaxSize;
            private readonly TimeSpan _gracefulShutdownDeadline;
            private readonly int _sendSourceInitialCapacity;
            private readonly byte _handshakeRequestFrameType;
            private readonly byte _handshakeRejectFrameType;

            private readonly byte[] _prefix = new byte[PrefixSize];
            private IMultiRefByteArray? _body;
            private Timer? _timer;
            private int _prefixRead;
            private int _bodyRead;
            private int _bodyLength;
            private int _completed;

            public AcceptHandshake(
                string transportName,
                Socket socket,
                Func<IEndPoint, UnionDataList, HandshakeOutcome> handlerFactory,
                string handshakeName,
                int handshakeMaxByteSize,
                int messageMaxByteSize,
                long bufferCapacityBytes,
                IMemoryRental memory,
                ILogger log,
                TimeSpan disconnectTimeout,
                TimeSpan rejectLinger,
                Action<TcpHandshakeResult> onCompleted,
                TimeSpan handshakeTimeout,
                RemoteHalfClosePolicy remoteHalfClosePolicy,
                int receiveBufferSize,
                int messagePartMaxSize,
                TimeSpan gracefulShutdownDeadline,
                int sendSourceInitialCapacity,
                byte handshakeRequestFrameType,
                byte handshakeRejectFrameType)
            {
                _transportName = transportName;
                _socket = socket;
                _handlerFactory = handlerFactory;
                _handshakeName = handshakeName;
                _handshakeMaxByteSize = handshakeMaxByteSize;
                _messageMaxByteSize = messageMaxByteSize;
                _bufferCapacityBytes = bufferCapacityBytes;
                _memory = memory;
                _log = log;
                _disconnectTimeout = disconnectTimeout;
                _rejectLinger = rejectLinger;
                _onCompleted = onCompleted;
                _handshakeTimeout = handshakeTimeout;
                _remoteHalfClosePolicy = remoteHalfClosePolicy;
                _receiveBufferSize = receiveBufferSize;
                _messagePartMaxSize = messagePartMaxSize;
                _gracefulShutdownDeadline = gracefulShutdownDeadline;
                _sendSourceInitialCapacity = sendSourceInitialCapacity;
                _handshakeRequestFrameType = handshakeRequestFrameType;
                _handshakeRejectFrameType = handshakeRejectFrameType;
            }

            public void Start()
            {
                try
                {
                    var timer = new Timer(
                        OnHandshakeDeadline, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
                    _timer = timer;
                    timer.Change(_handshakeTimeout, Timeout.InfiniteTimeSpan);
                    BeginReadPrefix();
                }
                catch (Exception ex)
                {
                    Reject(TcpHandshakeResult.Faulted(_transportName, ex));
                }
            }

            private void OnHandshakeDeadline(object? state)
            {
                Reject(TcpHandshakeResult.TimedOut(_transportName));
            }

            private void BeginReadPrefix()
            {
                try
                {
                    _socket.BeginReceive(
                        _prefix,
                        _prefixRead,
                        PrefixSize - _prefixRead,
                        SocketFlags.None,
                        OnPrefixReceived,
                        null);
                }
                catch (Exception ex)
                {
                    Reject(TcpHandshakeResult.Faulted(_transportName, ex));
                }
            }

            private void OnPrefixReceived(IAsyncResult ar)
            {
                int received;
                try
                {
                    received = _socket.EndReceive(ar);
                }
                catch (Exception ex)
                {
                    Reject(TcpHandshakeResult.Faulted(_transportName, ex));
                    return;
                }

                if (Volatile.Read(ref _completed) != 0)
                {
                    return;
                }

                if (received == 0)
                {
                    Reject(TcpHandshakeResult.EndOfStream(_transportName));
                    return;
                }

                _prefixRead += received;
                if (_prefixRead < PrefixSize)
                {
                    BeginReadPrefix();
                    return;
                }

                var size = new UnionDataMemoryAlias
                {
                    Byte0 = _prefix[0],
                    Byte1 = _prefix[1],
                    Byte2 = _prefix[2],
                    Byte3 = _prefix[3]
                };

                int length = size.IntValue;
                if (length <= 0)
                {
                    Reject(TcpHandshakeResult.ProtocolFailure(
                        _transportName, "Invalid handshake payload length"), SendProtocolReject("bad request"));
                    return;
                }

                // Reject before allocation: a hostile length prefix must not force a large buffer.
                if (length > _handshakeMaxByteSize)
                {
                    Reject(TcpHandshakeResult.PayloadTooBig(_transportName, _handshakeMaxByteSize));
                    return;
                }

                IMultiRefByteArray body;
                try
                {
                    body = _memory.ByteArraysPool.Acquire(length);
                }
                catch (Exception ex)
                {
                    Reject(TcpHandshakeResult.Faulted(_transportName, ex));
                    return;
                }

                _body = body;
                _bodyLength = length;
                _bodyRead = 0;
                BeginReadBody();
            }

            private void BeginReadBody()
            {
                var body = _body;
                if (body == null)
                {
                    Reject(TcpHandshakeResult.ProtocolFailure(
                        _transportName, "Handshake payload buffer is missing"));
                    return;
                }

                try
                {
                    _socket.BeginReceive(
                        body.Array,
                        body.Offset + _bodyRead,
                        _bodyLength - _bodyRead,
                        SocketFlags.None,
                        OnBodyReceived,
                        null);
                }
                catch (Exception ex)
                {
                    ReleaseBody();
                    Reject(TcpHandshakeResult.Faulted(_transportName, ex));
                }
            }

            private void OnBodyReceived(IAsyncResult ar)
            {
                int received;
                try
                {
                    received = _socket.EndReceive(ar);
                }
                catch (Exception ex)
                {
                    ReleaseBody();
                    Reject(TcpHandshakeResult.Faulted(_transportName, ex));
                    return;
                }

                if (Volatile.Read(ref _completed) != 0)
                {
                    ReleaseBody();
                    return;
                }

                if (received == 0)
                {
                    ReleaseBody();
                    Reject(TcpHandshakeResult.EndOfStream(_transportName));
                    return;
                }

                _bodyRead += received;
                if (_bodyRead < _bodyLength)
                {
                    BeginReadBody();
                    return;
                }

                CompleteBody();
            }

            private void CompleteBody()
            {
                var body = _body!;
                UnionDataList? requestData = null;
                bool deserialized;
                try
                {
                    var source = new ByteSourceFromArray(body);
                    requestData = _memory.CollectablePool.Acquire<UnionDataList>();
                    deserialized = requestData.Deserialize(ref source, _memory.ByteArraysPool);
                }
                catch (Exception ex)
                {
                    ReleaseBody();
                    requestData?.Release();
                    Reject(TcpHandshakeResult.Faulted(_transportName, ex));
                    return;
                }

                ReleaseBody();

                if (!deserialized)
                {
                    requestData!.Release();
                    Reject(TcpHandshakeResult.ProtocolFailure(
                            _transportName, "Failed to deserialize handshake payload"),
                        SendProtocolReject("bad request"));
                    return;
                }

                if (!requestData!.TryPopFirst(out byte frameType) ||
                    frameType != _handshakeRequestFrameType)
                {
                    requestData.Release();
                    Reject(TcpHandshakeResult.ProtocolFailure(
                            _transportName, "Unexpected handshake frame type"),
                        SendProtocolReject("bad request"));
                    return;
                }

                EndPoint? socketEndPoint;
                try
                {
                    socketEndPoint = _socket.RemoteEndPoint;
                }
                catch (Exception ex)
                {
                    requestData.Release();
                    Reject(TcpHandshakeResult.Faulted(_transportName, ex));
                    return;
                }

                if (socketEndPoint == null)
                {
                    requestData.Release();
                    Reject(TcpHandshakeResult.ProtocolFailure(
                        _transportName, "Accepted socket has no remote endpoint"));
                    return;
                }

                IpEndPoint remoteEndPoint;
                try
                {
                    remoteEndPoint = new IpEndPoint(socketEndPoint);
                }
                catch (Exception ex)
                {
                    requestData.Release();
                    Reject(TcpHandshakeResult.Faulted(_transportName, ex));
                    return;
                }

                // Claim completion before invoking user code so a racing deadline cannot deliver a
                // second result and orphan the endpoint.
                if (Interlocked.CompareExchange(ref _completed, 1, 0) != 0)
                {
                    requestData.Release();
                    return;
                }

                IRawReliableServerHandler? handler = null;
                UnionDataList? response = null;
                try
                {
                    var outcome = _handlerFactory(remoteEndPoint, requestData);
                    if (!outcome.IsAccepted)
                    {
                        var status = outcome.Status;
                        var text = RawReliableHandshake.FormatRejectText(
                            _handshakeName, RawReliableHandshake.Version, status);
                        CompleteClaimed(
                            TcpHandshakeResult.Rejected(MapOutcomeReason(status, text)),
                            SendOutcomeReject(status, text));
                        return;
                    }

                    handler = outcome.Handler!;
                    response = outcome.Response;
                    if (response == null)
                    {
                        CompleteClaimed(TcpHandshakeResult.Faulted(
                            _transportName,
                            new InvalidOperationException("The handshake outcome has no response"),
                            handler));
                        return;
                    }

                    var endpoint = new RawReliableTcpServerEndpoint(
                        _transportName,
                        remoteEndPoint,
                        _socket,
                        handler,
                        response,
                        outcome.AgreedMaxByteSize,
                        _bufferCapacityBytes,
                        _memory,
                        _log,
                        _disconnectTimeout,
                        _remoteHalfClosePolicy,
                        _receiveBufferSize,
                        _messagePartMaxSize,
                        _gracefulShutdownDeadline,
                        _sendSourceInitialCapacity);

                    // Ownership of the handshake response transfers to the endpoint.
                    response = null;

                    CompleteClaimed(TcpHandshakeResult.Accepted(endpoint, handler));
                }
                catch (Exception ex)
                {
                    response?.Release();
                    response = null;
                    CompleteClaimed(TcpHandshakeResult.Faulted(_transportName, ex, handler));
                }
                finally
                {
                    requestData.Release();
                }
            }

            private StopReason MapOutcomeReason(
                RawReliableHandshake.HandshakeStatus status, string text)
            {
                switch (status)
                {
                    case RawReliableHandshake.HandshakeStatus.VersionMismatch:
                    case RawReliableHandshake.HandshakeStatus.NameMismatch:
                        return new ProtocolMismatch(_transportName, text);
                    case RawReliableHandshake.HandshakeStatus.Rejected:
                        return new HandshakeRejected(_transportName, text);
                    case RawReliableHandshake.HandshakeStatus.CapacityExceeded:
                        return new HandshakeRejected(_transportName, text);
                    case RawReliableHandshake.HandshakeStatus.Timeout:
                        return new TimeOut(_transportName);
                    default:
                        return new ProtocolMismatch(_transportName, text);
                }
            }

            private UnionDataList? SendOutcomeReject(
                RawReliableHandshake.HandshakeStatus status, string text)
            {
                try
                {
                    var reject = RawReliableHandshake.BuildReject(
                        RawReliableHandshake.Version, _handshakeName, status, text);
                    reject.PutFirst(_handshakeRejectFrameType);
                    return reject;
                }
                catch (Exception ex)
                {
                    _log.wtf(ex);
                    return null;
                }
            }

            private UnionDataList? SendProtocolReject(string text)
            {
                return SendOutcomeReject(RawReliableHandshake.HandshakeStatus.Rejected, text);
            }

            private void Reject(TcpHandshakeResult result, UnionDataList? rejectPayload = null)
            {
                if (Interlocked.CompareExchange(ref _completed, 1, 0) != 0)
                {
                    rejectPayload?.Release();
                    return;
                }

                CompleteClaimed(result, rejectPayload);
            }

            private void CompleteClaimed(TcpHandshakeResult result, UnionDataList? rejectPayload = null)
            {
                // Release a body buffer that was still being read when the deadline won. The late APM
                // callback's ReleaseBody is Interlocked-guarded and becomes a no-op. The accepted path has
                // already released the body before claiming, so this is a no-op there as well.
                ReleaseBody();

                StopTimer();

                if (result.Status != TcpHandshakeStatus.Accepted)
                {
                    if (rejectPayload != null)
                    {
                        WriteReject(rejectPayload);
                        rejectPayload.Release();
                    }

                    CloseSocket();
                }

                try
                {
                    _onCompleted(result);
                }
                catch (Exception ex)
                {
                    _log.wtf(ex);
                }
            }

            private void WriteReject(UnionDataList reject)
            {
                IMultiRefByteArray? frame = null;
                try
                {
                    frame = UnionDataListCompositor.Encode(reject, _memory.ByteArraysPool);
                    if (frame == null)
                    {
                        return;
                    }

                    int offset = frame.Offset;
                    int remaining = frame.Count;
                    while (remaining > 0)
                    {
                        int sent = _socket.Send(frame.Array, offset, remaining, SocketFlags.None);
                        if (sent <= 0)
                        {
                            return;
                        }

                        offset += sent;
                        remaining -= sent;
                    }

                    if (_rejectLinger > TimeSpan.Zero)
                    {
                        Thread.Sleep(_rejectLinger);
                    }
                }
                catch (Exception ex)
                {
                    _log.wtf(ex);
                }
                finally
                {
                    frame?.Release();
                }
            }

            private void StopTimer()
            {
                var timer = Interlocked.Exchange(ref _timer, null);
                if (timer == null)
                {
                    return;
                }

                try
                {
                    timer.Dispose();
                }
                catch (Exception ex)
                {
                    _log.wtf(ex);
                }
            }

            private void ReleaseBody()
            {
                var body = Interlocked.Exchange(ref _body, null);
                if (body == null)
                {
                    return;
                }

                try
                {
                    body.Release();
                }
                catch (Exception ex)
                {
                    _log.wtf(ex);
                }
            }

            private void CloseSocket()
            {
                try
                {
                    _socket.LingerState = new LingerOption(true, 0);
                }
                catch (ObjectDisposedException)
                {
                }
                catch (Exception ex)
                {
                    _log.wtf(ex);
                }

                try
                {
                    _socket.Dispose();
                }
                catch (Exception ex)
                {
                    _log.wtf(ex);
                }
            }
        }
    }
}
