using System.Net.Sockets;
using Actuarius.Memory;
using Pontifex.NetSockets.Tcp;

namespace Pontifex.Tests.NetSockets.LowLevelTcpSocketTests.Harness
{
    /// <summary>
    /// Wires a <see cref="LowLevelTcpSocket"/> to deterministic doubles: a <see cref="FakeSocketAccessor"/>,
    /// a <see cref="ControllableQueue"/> source, a buffer pool, a <see cref="ReceiveSink"/> and a captured
    /// callback-error list. Convenience methods drive pumps, completions and the receive path.
    /// </summary>
    internal sealed class LowLevelSocketHarness : IDisposable, IAsyncDisposable
    {
        private readonly Action<Exception>? _userCallbackError;

        public LowLevelSocketHarness(
            IConcurrentPool<IMultiRefByteArray, int>? pool = null,
            RemoteHalfClosePolicy policy = RemoteHalfClosePolicy.KeepSendOpen,
            Action<Exception>? onCallbackError = null,
            int receiveBufferSize = 16 * 1024,
            TimeSpan? gracefulShutdownDeadline = null,
            IDeadlineScheduler? deadlineScheduler = null,
            ITeardownExecutor? teardownExecutor = null,
            IDeadlineScheduler? disposalScheduler = null)
        {
            Policy = policy;
            _userCallbackError = onCallbackError;
            Accessor = new FakeSocketAccessor();
            Queue = new ControllableQueue();
            Sink = new ReceiveSink();

            if (pool is null)
            {
                Controllable = new ControllablePool();
                Pool = Controllable;
            }
            else
            {
                Pool = pool;
                Controllable = pool as ControllablePool;
            }

            Socket = new LowLevelTcpSocket(
                Accessor, Queue, Sink.Handle, Pool, policy, OnCallbackError,
                receiveBufferSize, gracefulShutdownDeadline, deadlineScheduler,
                teardownExecutor, disposalScheduler);
        }

        public RemoteHalfClosePolicy Policy { get; }

        public FakeSocketAccessor Accessor { get; }

        public ControllableQueue Queue { get; }

        public IConcurrentPool<IMultiRefByteArray, int> Pool { get; }

        /// <summary>Non-null when the harness owns a <see cref="ControllablePool"/> (the default).</summary>
        public ControllablePool? Controllable { get; }

        public ReceiveSink Sink { get; }

        public List<Exception> CallbackErrors { get; } = new();

        public LowLevelTcpSocket Socket { get; }

        public ITcpSocketSender Sender => Socket.Sender;

        public ITcpSocketReceiver Receiver => Socket.Receiver;

        public TestSocketAsyncEventArgs? LastSend => Accessor.LastSendArgs;

        public TestSocketAsyncEventArgs? LastReceive => Accessor.LastReceiveArgs;

        public void EnqueueSend(byte[] data) => Queue.Put(new MultiRefByteArray(data));

        public void EnqueueSend(IMultiRefByteArray buffer) => Queue.Put(buffer);

        public void Pump() => Socket.Sender.Pump();

        public void ShutdownSender(bool graceful) => Socket.Sender.Shutdown(graceful);

        public void ShutdownSender(bool graceful, TimeSpan deadline) => Socket.Sender.Shutdown(graceful, deadline);

        public void StartReceiver() => Socket.Receiver.Start();

        /// <summary>Completes the most recent send operation (success or error).</summary>
        public TestSocketAsyncEventArgs CompleteSend(int bytesTransferred, SocketError error = SocketError.Success)
        {
            var args = Accessor.LastSendArgs
                       ?? throw new InvalidOperationException("No send operation was issued.");
            args.Complete(bytesTransferred, error);
            return args;
        }

        /// <summary>Completes the most recent receive operation (success or error).</summary>
        public TestSocketAsyncEventArgs CompleteReceive(int bytesTransferred, SocketError error = SocketError.Success)
        {
            var args = Accessor.LastReceiveArgs
                       ?? throw new InvalidOperationException("No receive operation was issued.");
            args.Complete(bytesTransferred, error);
            return args;
        }

        /// <summary>Copies <paramref name="data"/> into the pending receive buffer and completes the receive with its length.</summary>
        public TestSocketAsyncEventArgs DeliverReceive(byte[] data)
        {
            var args = Accessor.LastReceiveArgs
                       ?? throw new InvalidOperationException("No receive operation was issued.");
            var buffer = args.Buffer
                         ?? throw new InvalidOperationException("The receive operation has no buffer set.");

            int count = Math.Min(data.Length, args.Count);
            Buffer.BlockCopy(data, 0, buffer, args.Offset, count);
            args.Complete(count, SocketError.Success);
            return args;
        }

        /// <summary>Completes the pending receive as a zero-byte transfer (remote EOF / connection close).</summary>
        public TestSocketAsyncEventArgs CompleteReceiveEof()
            => CompleteReceive(0, SocketError.Success);

        public void Dispose() => Socket.Dispose();

        public ValueTask DisposeAsync() => Socket.DisposeAsync();

        public ValueTask DisposeAsync(TimeSpan timeout) => Socket.DisposeAsync(timeout);

        /// <summary>
        /// Explicit bounded teardown for tests: completes only the fake native operations that are
        /// still outstanding, then verifies none remain. Disposing the harness or the socket does not
        /// fabricate completions, so tests that leave a rearmed operation pending must call this (or
        /// document why the retained native-pending resource is intentional).
        /// </summary>
        public int CompletePendingOperations() => Accessor.CompletePendingOperations();

        private void OnCallbackError(Exception error)
        {
            lock (CallbackErrors)
            {
                CallbackErrors.Add(error);
            }

            _userCallbackError?.Invoke(error);
        }
    }
}
