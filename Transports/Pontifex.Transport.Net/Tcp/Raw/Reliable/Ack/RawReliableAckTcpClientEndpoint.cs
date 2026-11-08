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

namespace Pontifex.Raw.Reliable.Ack
{
    /// <summary>
    /// Reliable acknowledgement TCP client endpoint built on <see cref="LowLevelTcpSocket"/>.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Thread safety</b>: Lifecycle hooks run on the base serial action queue; the
    /// outbound source has a single writer (that queue), while socket callbacks may run on socket or
    /// thread-pool threads.</description></item>
    /// <item><description><b>Other notes</b>: The endpoint creates and owns the socket and the low-level
    /// transport, keeping keep-alive disabled unless configured.</description></item>
    /// </list>
    /// </remarks>
    internal sealed class RawReliableAckTcpClientEndpoint : RawReliableAckClientEndpoint
    {
        private const int DefaultReceiveBufferSize = 16 * 1024;

        private static readonly TimeSpan DefaultConnectTimeout = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan DefaultGracefulShutdownDeadline = TimeSpan.FromSeconds(10);

        private readonly IpEndPoint _remoteEndPoint;
        private readonly RemoteHalfClosePolicy _remoteHalfClosePolicy;
        private readonly int _receiveBufferSize;
        private readonly int _messagePartMaxSize;
        private readonly TimeSpan _connectTimeout;
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
        private CancellationTokenSource? _connectCts;

        private int _connectCompleted;
        private int _teardownStarted;
        private int _finishDisconnectIssued;
        private volatile bool _connectSucceeded;
        private volatile bool _connectFailed;
        private volatile bool _failedHard;
        private bool _receiveDisposed;

        internal RawReliableAckTcpClientEndpoint(
            string transportName,
            IpEndPoint remoteEndPoint,
            IRawReliableAckClientHandler handler,
            int messageMaxByteSize,
            long bufferCapacityBytes,
            IMemoryRental memory,
            ILogger log,
            PingOptions? pingOptions,
            RemoteHalfClosePolicy remoteHalfClosePolicy = RemoteHalfClosePolicy.GracefulSendShutdown,
            int receiveBufferSize = DefaultReceiveBufferSize,
            int messagePartMaxSize = DefaultReceiveBufferSize,
            TimeSpan? connectTimeout = null,
            TimeSpan? gracefulShutdownDeadline = null,
            int sendSourceInitialCapacity = 10)
            : base(
                transportName,
                remoteEndPoint,
                handler,
                messageMaxByteSize,
                bufferCapacityBytes,
                memory,
                log,
                pingOptions)
        {
            if (remoteEndPoint == null)
            {
                throw new ArgumentNullException(nameof(remoteEndPoint));
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

            _connectTimeout = connectTimeout ?? DefaultConnectTimeout;
            if (_connectTimeout <= TimeSpan.Zero || _connectTimeout > LowLevelTcpSocket.MaxFiniteDeadline)
            {
                throw new ArgumentOutOfRangeException(nameof(connectTimeout));
            }

            _gracefulShutdownDeadline = gracefulShutdownDeadline ?? DefaultGracefulShutdownDeadline;
            if (_gracefulShutdownDeadline != Timeout.InfiniteTimeSpan &&
                (_gracefulShutdownDeadline < TimeSpan.Zero ||
                 _gracefulShutdownDeadline > LowLevelTcpSocket.MaxFiniteDeadline))
            {
                throw new ArgumentOutOfRangeException(nameof(gracefulShutdownDeadline));
            }

            _remoteEndPoint = remoteEndPoint;
            _remoteHalfClosePolicy = remoteHalfClosePolicy;
            _receiveBufferSize = receiveBufferSize;
            _messagePartMaxSize = messagePartMaxSize;
            _sendSourceInitialCapacity = sendSourceInitialCapacity;
        }

        /// <summary>
        /// Starts the socket connection attempt without blocking the action queue.
        /// </summary>
        protected override void BeginConnect()
        {
            try
            {
                _source = new SingleReaderWriterConcurrentQueue<IMultiRefByteArray>(_sendSourceInitialCapacity);
                _receiveCompositor = new UnionDataListCompositor(
                    OnFramedMessage, Memory.CollectablePool, Memory.ByteArraysPool, MessageMaxByteSize);

                var socket = new Socket(_remoteEndPoint.EP.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
                {
                    NoDelay = true
                };
                _socket = socket;

                StartConnectDeadline();

                socket.BeginConnect(_remoteEndPoint.EP, OnConnectCallback, null);
            }
            catch (Exception ex)
            {
                FailConnect(ex);
            }
        }

        /// <summary>
        /// Starts receiving only once the reliable stage is connected, so early inbound bytes are not
        /// mistaken for a protocol parse failure, then runs the inherited handshake/keep-alive.
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
                AbortPendingConnect();

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

        private void StartConnectDeadline()
        {
            var cts = new CancellationTokenSource();
            _connectCts = cts;

            _ = Task.Delay(_connectTimeout, cts.Token).ContinueWith(
                _ => FailConnect(new TimeoutException($"Connect timeout after {_connectTimeout}")),
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnRanToCompletion,
                TaskScheduler.Default);
        }

        private void CancelConnectDeadline()
        {
            var cts = Interlocked.Exchange(ref _connectCts, null);
            if (cts == null)
            {
                return;
            }

            try
            {
                cts.Cancel();
            }
            catch (Exception ex)
            {
                Log.wtf(ex);
            }

            TryDispose(cts);
        }

        private void OnConnectCallback(IAsyncResult ar)
        {
            var socket = _socket;
            if (socket == null)
            {
                return;
            }

            try
            {
                socket.EndConnect(ar);
            }
            catch (Exception ex)
            {
                FailConnect(ex);
                return;
            }

            if (Interlocked.Exchange(ref _connectCompleted, 1) != 0)
            {
                TryDispose(socket);
                return;
            }

            try
            {
                CancelConnectDeadline();

                if (_failedHard)
                {
                    TryDispose(socket);
                    _socket = null;
                    return;
                }

                ConnectLowLevel(socket);
            }
            catch (Exception ex)
            {
                FailConnectOwned(ex);
            }
        }

        private void ConnectLowLevel(Socket socket)
        {
            // The connect one-shot is already won by this path, so failures use the owned variant.
            ISocketAccessor? accessor = null;
            LowLevelTcpSocket? lowLevel = null;

            try
            {
                accessor = new SystemSocket(socket);
                lowLevel = new LowLevelTcpSocket(
                    accessor,
                    _source!,
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

                bool abort;
                lock (_lifecycleGate)
                {
                    abort = _failedHard;
                    if (!abort)
                    {
                        _connectSucceeded = true;
                    }
                }

                if (abort)
                {
                    TryDispose(lowLevel);
                    _lowLevel = null;
                    DisposeReceiveCompositor();
                    return;
                }

                // If a stop landed while connecting, the production serial queue runs the nested
                // BeginStop action before FinishConnect can observe the stop, so BeginDisconnect is
                // skipped and this callback is the only cleanup hook.
                FinishConnect(BeginAbortLowLevel);
            }
            catch (Exception ex)
            {
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
                FailConnectOwned(ex);
            }
        }

        private void FailConnect(Exception ex)
        {
            if (_failedHard)
            {
                return;
            }

            if (Interlocked.Exchange(ref _connectCompleted, 1) != 0)
            {
                return;
            }

            FailConnectOwned(ex);
        }

        private void FailConnectOwned(Exception ex)
        {
            _connectFailed = true;

            if (_failedHard)
            {
                CancelConnectDeadline();
                TryDispose(_socket);
                _socket = null;
                return;
            }

            CancelConnectDeadline();
            TryDispose(_socket);
            _socket = null;
            _connectSucceeded = false;

            Stop(new ExceptionFail(TransportName, ex, "Connect failed"), false);

            // FinishStart only runs from FinishConnect. Without it the endpoint would stay in Starting.
            FinishConnect(BeginAbortLowLevel);
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

        private void AbortPendingConnect()
        {
            if (Interlocked.CompareExchange(ref _connectCompleted, 1, 0) != 0)
            {
                return;
            }

            CancelConnectDeadline();
            TryDispose(_socket);
            _socket = null;
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
