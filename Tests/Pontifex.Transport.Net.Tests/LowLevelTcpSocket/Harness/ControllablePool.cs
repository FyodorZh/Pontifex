using Actuarius.Memory;

namespace Pontifex.Tests.NetSockets.LowLevelTcpSocketTests.Harness
{
    /// <summary>
    /// Deterministic <see cref="IConcurrentPool{TResource, TParam0}"/> for injection tests. It hands
    /// out queued/crafted buffers, can be scripted to throw or run a hook on <see cref="Acquire"/>, and
    /// records the requested sizes.
    /// </summary>
    /// <remarks>
    /// This is deliberately different from <c>ByteArrayConcurrentPoolGuard</c>: the guard instruments a
    /// real pool for leak detection but cannot control what is returned. This pool controls the input.
    /// </remarks>
    internal sealed class ControllablePool : IConcurrentPool<IMultiRefByteArray, int>
    {
        private readonly object _sync = new();
        private readonly Queue<IMultiRefByteArray> _queued = new();
        private readonly List<int> _requestedSizes = new();

        private int _acquireCount;
        private int _releaseCount;

        /// <summary>Factory used when the queue is empty. Defaults to <c>new MultiRefByteArray(new byte[param0])</c>.</summary>
        public Func<int, IMultiRefByteArray>? Factory { get; set; }

        /// <summary>When set, <see cref="Acquire"/> throws this exception instead of returning a buffer.</summary>
        public Exception? AcquireException { get; set; }

        /// <summary>Runs at the start of <see cref="Acquire"/>; useful to block or observe.</summary>
        public Action<int>? OnAcquire { get; set; }

        public int AcquireCount => Volatile.Read(ref _acquireCount);

        public int ReleaseCount => Volatile.Read(ref _releaseCount);

        public int QueuedCount
        {
            get { lock (_sync) { return _queued.Count; } }
        }

        public int[] RequestedSizes
        {
            get { lock (_sync) { return _requestedSizes.ToArray(); } }
        }

        /// <summary>Adds a buffer that the next <see cref="Acquire"/> calls will return, in FIFO order.</summary>
        public void Enqueue(IMultiRefByteArray buffer)
        {
            lock (_sync)
            {
                _queued.Enqueue(buffer);
            }
        }

        public IMultiRefByteArray Acquire(int param0)
        {
            Interlocked.Increment(ref _acquireCount);
            lock (_sync)
            {
                _requestedSizes.Add(param0);
            }

            OnAcquire?.Invoke(param0);

            if (AcquireException != null)
            {
                throw AcquireException;
            }

            lock (_sync)
            {
                if (_queued.Count > 0)
                {
                    return _queued.Dequeue();
                }
            }

            return Factory?.Invoke(param0) ?? new MultiRefByteArray(new byte[param0]);
        }

        public (IMultiRefByteArray resource, IPoolSink<IMultiRefByteArray> poolSink) AcquireEx(int param0)
            => (Acquire(param0), this);

        /// <summary>
        /// Mirrors the real <c>ByteArrayPool.Release</c>: a no-op. Buffers return to their source via
        /// their own reference-counted <c>Release()</c>, not through the pool sink.
        /// </summary>
        public void Release(IMultiRefByteArray? resource)
        {
            Interlocked.Increment(ref _releaseCount);
        }
    }
}
