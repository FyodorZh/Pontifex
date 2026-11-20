using System;
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
    /// <item><description><b>No handshake</b>: the accepted socket is already connected, so the logical connection
    /// completes together with the low-level transport setup. There is no acknowledgement request, response, or
    /// deadline to wait for.</description></item>
    /// <item><description><b>Other notes</b>: The endpoint takes ownership of an already-connected socket that the
    /// carrier accepted. It is created non-started; the caller must call Start or Stop. There is no
    /// <c>ToString</c> override: the transport owns the <c>tcp-noack-server[...]</c> label.</description></item>
    /// </list>
    /// </remarks>
    internal sealed class RawReliableTcpServerEndpoint : RawReliableHandshakeServerEndpoint
    {
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
        /// server transport creates the endpoint directly from its accept callback; there is no asynchronous
        /// handshake factory.</description></item>
        /// </list>
        /// </remarks>
        internal RawReliableTcpServerEndpoint(
            string transportName,
            IEndPoint remoteEndPoint,
            Socket socket,
            IRawReliableServerHandler handler,
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
        /// Notifies the handler of the logical connection and starts the inherited idle-session watchdog, then
        /// starts receiving. There is no handshake, so the connection completes together with the socket setup.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Called on the serial action queue.</description></item>
        /// <item><description><b>Other notes</b>: The receiver starts only when the endpoint is still running after
        /// the handler notification. A handler exception or a handler-initiated disconnect stops the endpoint
        /// synchronously, so the receiver must not be started for it.</description></item>
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
    }
}
