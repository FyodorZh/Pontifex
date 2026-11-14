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

namespace Pontifex.Raw.Reliable.Ack
{
    /// <summary>
    /// The terminal status of a reliable acknowledgement server-side accept attempt.
    /// </summary>
    internal enum AckAcceptStatus
    {
        /// <summary>
        /// The acknowledgement handshake succeeded and a non-started endpoint was created.
        /// </summary>
        Accepted,

        /// <summary>
        /// The handler factory returned <see langword="null"/> and the connection was closed.
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
        /// The advertised acknowledgement payload length exceeded the configured maximum; the body was
        /// never allocated.
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
    /// The result of a reliable acknowledgement server-side accept attempt.
    /// </summary>
    internal readonly struct AckAcceptResult
    {
        /// <summary>
        /// Gets the terminal status of the accept attempt.
        /// </summary>
        public AckAcceptStatus Status { get; }

        /// <summary>
        /// Gets the created endpoint. Non-null if and only if <see cref="Status"/> is
        /// <see cref="AckAcceptStatus.Accepted"/>.
        /// </summary>
        public RawReliableAckTcpServerEndpoint? Endpoint { get; }

        /// <summary>
        /// Gets the stop reason associated with a rejection or protocol failure, when available.
        /// </summary>
        public StopReason? Reason { get; }

        /// <summary>
        /// Gets the exception that caused a fault, when available.
        /// </summary>
        public Exception? Error { get; }

        internal AckAcceptResult(
            AckAcceptStatus status,
            RawReliableAckTcpServerEndpoint? endpoint,
            StopReason? reason,
            Exception? error)
        {
            Status = status;
            Endpoint = endpoint;
            Reason = reason;
            Error = error;
        }

        internal static AckAcceptResult Accepted(RawReliableAckTcpServerEndpoint endpoint)
        {
            return new AckAcceptResult(AckAcceptStatus.Accepted, endpoint, null, null);
        }

        internal static AckAcceptResult Rejected(StopReason reason)
        {
            return new AckAcceptResult(AckAcceptStatus.Rejected, null, reason, null);
        }

        internal static AckAcceptResult EndOfStream(string transportName)
        {
            return new AckAcceptResult(
                AckAcceptStatus.Eof, null, new UnknownRemoteIntention(transportName), null);
        }

        internal static AckAcceptResult TimedOut(string transportName)
        {
            return new AckAcceptResult(AckAcceptStatus.Timeout, null, new TimeOut(transportName), null);
        }

        internal static AckAcceptResult PayloadTooBig(string transportName)
        {
            return new AckAcceptResult(
                AckAcceptStatus.MessageTooBig,
                null,
                new TextFail(transportName, "Ack data exceeds AckMaxByteSize"),
                null);
        }

        internal static AckAcceptResult ProtocolFailure(string transportName, string text)
        {
            return new AckAcceptResult(
                AckAcceptStatus.ProtocolError, null, new TextFail(transportName, text), null);
        }

        internal static AckAcceptResult Faulted(string transportName, Exception error)
        {
            return new AckAcceptResult(
                AckAcceptStatus.Faulted,
                null,
                new ExceptionFail(transportName, error, "Ack handshake failed"),
                error);
        }
    }

    /// <summary>
    /// Reliable acknowledgement TCP server endpoint built on <see cref="LowLevelTcpSocket"/>.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Thread safety</b>: Lifecycle hooks run on the base serial action queue; the
    /// outbound source has a single writer (that queue), while socket callbacks may run on socket or
    /// thread-pool threads.</description></item>
    /// <item><description><b>Other notes</b>: The endpoint takes ownership of an already-connected socket
    /// whose acknowledgement request has been read and validated by <see cref="BeginAccept"/>. The
    /// endpoint is returned non-started; the caller must call Start or Stop.</description></item>
    /// </list>
    /// </remarks>
    internal sealed class RawReliableAckTcpServerEndpoint : RawReliableAckServerEndpoint
    {
        private const int DefaultReceiveBufferSize = 16 * 1024;

        private static readonly TimeSpan DefaultAckHandshakeTimeout = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan DefaultGracefulShutdownDeadline = TimeSpan.FromSeconds(10);

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

        private RawReliableAckTcpServerEndpoint(
            string transportName,
            IEndPoint remoteEndPoint,
            Socket socket,
            IRawReliableAckServerHandler handler,
            int messageMaxByteSize,
            long bufferCapacityBytes,
            IMemoryRental memory,
            ILogger log,
            TimeSpan disconnectTimeout,
            UnionDataList pendingAckData,
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

            if (pendingAckData == null)
            {
                throw new ArgumentNullException(nameof(pendingAckData));
            }

            _disconnectTimeout = disconnectTimeout;
            _remoteHalfClosePolicy = remoteHalfClosePolicy;
            _receiveBufferSize = receiveBufferSize;
            _messagePartMaxSize = messagePartMaxSize;
            _gracefulShutdownDeadline = gracefulShutdownDeadline;
            _sendSourceInitialCapacity = sendSourceInitialCapacity;

            _socket = socket;
            SetPendingAckData(pendingAckData);
        }

        /// <summary>
        /// Reads the client's acknowledgement request from an accepted socket and creates a non-started
        /// endpoint when the handler factory admits the session.
        /// </summary>
        /// <param name="transportName">The name of the transport that owns the endpoint.</param>
        /// <param name="socket">The accepted, connected socket. Ownership transfers to this call on every
        /// outcome; the socket is closed when the handshake does not succeed.</param>
        /// <param name="handlerFactory">Creates the session handler from the remote endpoint and the borrowed
        /// acknowledgement request data. The data is owned by the endpoint after the callback returns: the
        /// factory must not retain it and must release any copy it makes. The factory must be fast and
        /// non-blocking, because once the completion is claimed the handshake deadline cannot preempt it. A
        /// null return rejects the session.</param>
        /// <param name="messageMaxByteSize">The inclusive maximum message size, in bytes, admitted by the
        /// endpoint; must be at least 1.</param>
        /// <param name="bufferCapacityBytes">The byte budget for messages accepted by the endpoint but not
        /// yet transmitted; must be at least 1.</param>
        /// <param name="memory">The memory rental system used to rent buffers.</param>
        /// <param name="log">The logging system used by the endpoint.</param>
        /// <param name="disconnectTimeout">The maximum time without an inbound message before the endpoint
        /// is stopped as timed out; must be positive.</param>
        /// <param name="onCompleted">The callback invoked exactly once with the accept result. It may run on
        /// an APM or thread-pool thread, or synchronously in the caller's thread for an immediate
        /// failure.</param>
        /// <param name="ackMaxByteSize">The inclusive maximum size of the acknowledgement payload; must be
        /// positive, no greater than <paramref name="messageMaxByteSize"/>, and no greater than the protocol
        /// constant <see cref="RawReliableAckInfo.AckMaxByteSize"/>. The value may be stricter than the
        /// protocol constant but not larger.</param>
        /// <param name="ackHandshakeTimeout">The overall deadline for reading the acknowledgement request;
        /// defaults to ten seconds.</param>
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
        /// <item><description><b>Preconditions/Postconditions</b>: The acknowledgement length prefix is
        /// validated before any body buffer is allocated, so an oversized advertisement cannot force a
        /// large allocation. A rejected or failed handshake closes the socket silently.</description></item>
        /// <item><description><b>Thread safety</b>: The callback is serialized by an internal guard and is
        /// invoked exactly once.</description></item>
        /// </list>
        /// </remarks>
        internal static void BeginAccept(
            string transportName,
            Socket socket,
            Func<IEndPoint, UnionDataList, IRawReliableAckServerHandler?> handlerFactory,
            int messageMaxByteSize,
            long bufferCapacityBytes,
            IMemoryRental memory,
            ILogger log,
            TimeSpan disconnectTimeout,
            Action<AckAcceptResult> onCompleted,
            int ackMaxByteSize = RawReliableAckInfo.AckMaxByteSize,
            TimeSpan? ackHandshakeTimeout = null,
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

            if (messageMaxByteSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(messageMaxByteSize));
            }

            if (bufferCapacityBytes < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(bufferCapacityBytes));
            }

            if (ackMaxByteSize <= 0 ||
                ackMaxByteSize > RawReliableAckInfo.AckMaxByteSize ||
                ackMaxByteSize > messageMaxByteSize)
            {
                throw new ArgumentOutOfRangeException(nameof(ackMaxByteSize));
            }

            if (disconnectTimeout <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(disconnectTimeout));
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

            var handshakeTimeout = ackHandshakeTimeout ?? DefaultAckHandshakeTimeout;
            if (handshakeTimeout <= TimeSpan.Zero || handshakeTimeout > LowLevelTcpSocket.MaxFiniteDeadline)
            {
                throw new ArgumentOutOfRangeException(nameof(ackHandshakeTimeout));
            }

            var shutdownDeadline = gracefulShutdownDeadline ?? DefaultGracefulShutdownDeadline;
            if (shutdownDeadline != Timeout.InfiniteTimeSpan &&
                (shutdownDeadline < TimeSpan.Zero || shutdownDeadline > LowLevelTcpSocket.MaxFiniteDeadline))
            {
                throw new ArgumentOutOfRangeException(nameof(gracefulShutdownDeadline));
            }

            var handshake = new AcceptHandshake(
                transportName,
                socket,
                handlerFactory,
                messageMaxByteSize,
                bufferCapacityBytes,
                memory,
                log,
                disconnectTimeout,
                onCompleted,
                ackMaxByteSize,
                handshakeTimeout,
                remoteHalfClosePolicy,
                receiveBufferSize,
                messagePartMaxSize,
                shutdownDeadline,
                sendSourceInitialCapacity,
                (byte)MessageType.AckRequestMessage);

            handshake.Start();
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
        /// Completes the acknowledgement handshake for the buffered request, then starts receiving so
        /// pipelined payload cannot overtake the acknowledgement completion.
        /// </summary>
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
        /// Rejects further sends, notifies the handler, and starts the socket teardown in the background.
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
                ReleasePendingAckData();

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
                ReleasePendingAckData();
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
                ReleasePendingAckData();

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
        /// Callback-driven acknowledgement handshake. Reads the four-byte length prefix, validates the
        /// advertised body length against the configured maximum before allocating, reads and deserializes
        /// the body, and invokes the handler factory. Uses APM and a single timer; no Task or async.
        /// </summary>
        private sealed class AcceptHandshake
        {
            private const int PrefixSize = 4;

            private readonly string _transportName;
            private readonly Socket _socket;
            private readonly Func<IEndPoint, UnionDataList, IRawReliableAckServerHandler?> _handlerFactory;
            private readonly int _messageMaxByteSize;
            private readonly long _bufferCapacityBytes;
            private readonly IMemoryRental _memory;
            private readonly ILogger _log;
            private readonly TimeSpan _disconnectTimeout;
            private readonly Action<AckAcceptResult> _onCompleted;
            private readonly int _ackMaxByteSize;
            private readonly TimeSpan _ackHandshakeTimeout;
            private readonly RemoteHalfClosePolicy _remoteHalfClosePolicy;
            private readonly int _receiveBufferSize;
            private readonly int _messagePartMaxSize;
            private readonly TimeSpan _gracefulShutdownDeadline;
            private readonly int _sendSourceInitialCapacity;
            private readonly byte _ackRequestMessageType;

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
                Func<IEndPoint, UnionDataList, IRawReliableAckServerHandler?> handlerFactory,
                int messageMaxByteSize,
                long bufferCapacityBytes,
                IMemoryRental memory,
                ILogger log,
                TimeSpan disconnectTimeout,
                Action<AckAcceptResult> onCompleted,
                int ackMaxByteSize,
                TimeSpan ackHandshakeTimeout,
                RemoteHalfClosePolicy remoteHalfClosePolicy,
                int receiveBufferSize,
                int messagePartMaxSize,
                TimeSpan gracefulShutdownDeadline,
                int sendSourceInitialCapacity,
                byte ackRequestMessageType)
            {
                _transportName = transportName;
                _socket = socket;
                _handlerFactory = handlerFactory;
                _messageMaxByteSize = messageMaxByteSize;
                _bufferCapacityBytes = bufferCapacityBytes;
                _memory = memory;
                _log = log;
                _disconnectTimeout = disconnectTimeout;
                _onCompleted = onCompleted;
                _ackMaxByteSize = ackMaxByteSize;
                _ackHandshakeTimeout = ackHandshakeTimeout;
                _remoteHalfClosePolicy = remoteHalfClosePolicy;
                _receiveBufferSize = receiveBufferSize;
                _messagePartMaxSize = messagePartMaxSize;
                _gracefulShutdownDeadline = gracefulShutdownDeadline;
                _sendSourceInitialCapacity = sendSourceInitialCapacity;
                _ackRequestMessageType = ackRequestMessageType;
            }

            public void Start()
            {
                try
                {
                    var timer = new Timer(
                        OnHandshakeDeadline, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
                    _timer = timer;
                    timer.Change(_ackHandshakeTimeout, Timeout.InfiniteTimeSpan);
                    BeginReadPrefix();
                }
                catch (Exception ex)
                {
                    Reject(AckAcceptResult.Faulted(_transportName, ex));
                }
            }

            private void OnHandshakeDeadline(object? state)
            {
                Reject(AckAcceptResult.TimedOut(_transportName));
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
                    Reject(AckAcceptResult.Faulted(_transportName, ex));
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
                    Reject(AckAcceptResult.Faulted(_transportName, ex));
                    return;
                }

                if (Volatile.Read(ref _completed) != 0)
                {
                    return;
                }

                if (received == 0)
                {
                    Reject(AckAcceptResult.EndOfStream(_transportName));
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
                    Reject(AckAcceptResult.ProtocolFailure(
                        _transportName, "Invalid acknowledgement payload length"));
                    return;
                }

                // Reject before allocation: a hostile length prefix must not force a large buffer.
                if (length > _ackMaxByteSize)
                {
                    Reject(AckAcceptResult.PayloadTooBig(_transportName));
                    return;
                }

                IMultiRefByteArray body;
                try
                {
                    body = _memory.ByteArraysPool.Acquire(length);
                }
                catch (Exception ex)
                {
                    Reject(AckAcceptResult.Faulted(_transportName, ex));
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
                    Reject(AckAcceptResult.ProtocolFailure(
                        _transportName, "Acknowledgement payload buffer is missing"));
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
                    Reject(AckAcceptResult.Faulted(_transportName, ex));
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
                    Reject(AckAcceptResult.Faulted(_transportName, ex));
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
                    Reject(AckAcceptResult.EndOfStream(_transportName));
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
                UnionDataList? ackData = null;
                bool deserialized;
                try
                {
                    var source = new ByteSourceFromArray(body);
                    ackData = _memory.CollectablePool.Acquire<UnionDataList>();
                    deserialized = ackData.Deserialize(ref source, _memory.ByteArraysPool);
                }
                catch (Exception ex)
                {
                    ReleaseBody();
                    ackData?.Release();
                    Reject(AckAcceptResult.Faulted(_transportName, ex));
                    return;
                }

                ReleaseBody();

                if (!deserialized)
                {
                    ackData!.Release();
                    Reject(AckAcceptResult.ProtocolFailure(
                        _transportName, "Failed to deserialize acknowledgement payload"));
                    return;
                }

                if (!ackData!.TryPopFirst(out byte frameType) || frameType != _ackRequestMessageType)
                {
                    ackData.Release();
                    Reject(AckAcceptResult.ProtocolFailure(
                        _transportName, "Unexpected acknowledgement frame type"));
                    return;
                }

                EndPoint? socketEndPoint;
                try
                {
                    socketEndPoint = _socket.RemoteEndPoint;
                }
                catch (Exception ex)
                {
                    ackData.Release();
                    Reject(AckAcceptResult.Faulted(_transportName, ex));
                    return;
                }

                if (socketEndPoint == null)
                {
                    ackData.Release();
                    Reject(AckAcceptResult.ProtocolFailure(
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
                    ackData.Release();
                    Reject(AckAcceptResult.Faulted(_transportName, ex));
                    return;
                }

                // Claim completion before invoking user code so a racing deadline cannot deliver a
                // second result and orphan the endpoint.
                if (Interlocked.CompareExchange(ref _completed, 1, 0) != 0)
                {
                    ackData.Release();
                    return;
                }

                IRawReliableAckServerHandler? handler;
                try
                {
                    handler = _handlerFactory(remoteEndPoint, ackData);
                }
                catch (Exception ex)
                {
                    ackData.Release();
                    CompleteClaimed(AckAcceptResult.Faulted(_transportName, ex));
                    return;
                }

                if (handler == null)
                {
                    ackData.Release();
                    CompleteClaimed(AckAcceptResult.Rejected(new AckRejected(_transportName)));
                    return;
                }

                try
                {
                    var endpoint = new RawReliableAckTcpServerEndpoint(
                        _transportName,
                        remoteEndPoint,
                        _socket,
                        handler,
                        _messageMaxByteSize,
                        _bufferCapacityBytes,
                        _memory,
                        _log,
                        _disconnectTimeout,
                        ackData,
                        _remoteHalfClosePolicy,
                        _receiveBufferSize,
                        _messagePartMaxSize,
                        _gracefulShutdownDeadline,
                        _sendSourceInitialCapacity);

                    CompleteClaimed(AckAcceptResult.Accepted(endpoint));
                }
                catch (Exception ex)
                {
                    ackData.Release();
                    CompleteClaimed(AckAcceptResult.Faulted(_transportName, ex));
                }
            }

            private void Reject(AckAcceptResult result)
            {
                if (Interlocked.CompareExchange(ref _completed, 1, 0) != 0)
                {
                    return;
                }

                CompleteClaimed(result);
            }

            private void CompleteClaimed(AckAcceptResult result)
            {
                // Release a body buffer that was still being read when the deadline won. The late APM
                // callback's ReleaseBody is Interlocked-guarded and becomes a no-op. The accepted path has
                // already released the body before claiming, so this is a no-op there as well.
                ReleaseBody();

                StopTimer();

                if (result.Status != AckAcceptStatus.Accepted)
                {
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
