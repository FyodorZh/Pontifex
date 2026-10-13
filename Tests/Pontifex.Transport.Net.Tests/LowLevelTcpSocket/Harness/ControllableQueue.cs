using Actuarius.Collections;
using Actuarius.Memory;

namespace Pontifex.Tests.NetSockets.LowLevelTcpSocketTests.Harness
{
    /// <summary>
    /// Deterministic <see cref="ISingleReaderWriterConcurrentQueue{TData}"/> for tests. It counts
    /// pops/puts, exposes the items that are still queued, and can pause <see cref="TryPop"/> so a
    /// test can gate the sender's observation of an empty source.
    /// </summary>
    /// <remarks>
    /// The real source is an SPSC queue: one reader and one writer, never two of either. In
    /// <see cref="StrictMode"/> the double records overlapping readers/writers in
    /// <see cref="ReaderOverlapCount"/>/<see cref="WriterOverlapCount"/> instead of silently
    /// serializing the illegal access away. Concurrent single reader + single writer is legal and is
    /// never counted as an overlap.
    /// </remarks>
    internal sealed class ControllableQueue : ISingleReaderWriterConcurrentQueue<IMultiRefByteArray>
    {
        private readonly object _sync = new();
        private readonly Queue<IMultiRefByteArray> _items = new();
        private readonly ManualResetEventSlim _popGate = new(true);
        private readonly ManualResetEventSlim _putGate = new(true);

        private int _putCount;
        private int _popCount;
        private int _forceEmptyCount;
        private int _activeReaders;
        private int _activeWriters;
        private int _readerOverlapCount;
        private int _writerOverlapCount;

        /// <summary>
        /// When enabled, overlapping readers/writers are recorded. Off by default so intentionally
        /// serialized misuse in legacy tests is unaffected.
        /// </summary>
        public bool StrictMode { get; set; }

        /// <summary>
        /// Invoked when <see cref="TryPop"/> observes the queue empty, just before returning false.
        /// Lets a test publish data / request shutdown to script an exact interleaving (e.g. F01).
        /// </summary>
        public Action? OnEmptyObservation { get; set; }

        /// <summary>
        /// Invoked at the very start of <see cref="TryPop"/>, before the pause gate is awaited. Lets a
        /// test observe deterministically that a reader has entered the pop (e.g. it is about to block
        /// on <see cref="Pause"/>) without racing on timing.
        /// </summary>
        public Action? OnPopEntered { get; set; }

        /// <summary>
        /// Invoked at the very start of <see cref="Put"/>, before the put gate is awaited. Lets a test
        /// observe deterministically that a writer has entered the put.
        /// </summary>
        public Action? OnPutEntered { get; set; }

        /// <summary>Forces the next N pops to report empty without dequeuing (models a stale observation).</summary>
        public int ForceEmptyCount
        {
            get => Volatile.Read(ref _forceEmptyCount);
            set => Volatile.Write(ref _forceEmptyCount, value);
        }

        /// <summary>When set, <see cref="TryPop"/> throws this instead of dequeuing (models a failing producer/source).</summary>
        public Exception? PopException { get; set; }

        public int Count
        {
            get { lock (_sync) { return _items.Count; } }
        }

        public int PutCount => Volatile.Read(ref _putCount);

        public int PopCount => Volatile.Read(ref _popCount);

        /// <summary>Number of readers currently inside <see cref="TryPop"/> (including paused ones).</summary>
        public int ActiveReaders => Volatile.Read(ref _activeReaders);

        /// <summary>Number of writers currently inside <see cref="Put"/> (including paused ones).</summary>
        public int ActiveWriters => Volatile.Read(ref _activeWriters);

        /// <summary>Number of times a second reader entered <see cref="TryPop"/> concurrently (strict mode).</summary>
        public int ReaderOverlapCount => Volatile.Read(ref _readerOverlapCount);

        /// <summary>Number of times a second writer entered <see cref="Put"/> concurrently (strict mode).</summary>
        public int WriterOverlapCount => Volatile.Read(ref _writerOverlapCount);

        public bool IsPaused => !_popGate.IsSet;

        public bool IsPutPaused => !_putGate.IsSet;

        public bool Put(IMultiRefByteArray value)
        {
            int activeWriters = Interlocked.Increment(ref _activeWriters);
            if (StrictMode && activeWriters > 1)
            {
                Interlocked.Increment(ref _writerOverlapCount);
            }

            try
            {
                Interlocked.Increment(ref _putCount);
                OnPutEntered?.Invoke();
                _putGate.Wait();

                lock (_sync)
                {
                    _items.Enqueue(value);
                }

                return true;
            }
            finally
            {
                Interlocked.Decrement(ref _activeWriters);
            }
        }

        public bool TryPop(out IMultiRefByteArray value)
        {
            int activeReaders = Interlocked.Increment(ref _activeReaders);
            if (StrictMode && activeReaders > 1)
            {
                Interlocked.Increment(ref _readerOverlapCount);
            }

            try
            {
                if (PopException != null)
                {
                    throw PopException;
                }

                OnPopEntered?.Invoke();
                _popGate.Wait();

                if (Volatile.Read(ref _forceEmptyCount) > 0)
                {
                    Interlocked.Decrement(ref _forceEmptyCount);
                    value = null!;
                    return false;
                }

                lock (_sync)
                {
                    if (_items.Count > 0)
                    {
                        value = _items.Dequeue();
                        Interlocked.Increment(ref _popCount);
                        return true;
                    }
                }

                OnEmptyObservation?.Invoke();

                value = null!;
                return false;
            }
            finally
            {
                Interlocked.Decrement(ref _activeReaders);
            }
        }

        /// <summary>Blocks any <see cref="TryPop"/> call (present or future) until <see cref="Resume"/> is called.</summary>
        public void Pause() => _popGate.Reset();

        /// <summary>Lets <see cref="TryPop"/> calls blocked by <see cref="Pause"/> proceed.</summary>
        public void Resume() => _popGate.Set();

        /// <summary>Blocks any <see cref="Put"/> call (present or future) until <see cref="ResumePuts"/> is called.</summary>
        public void PausePuts() => _putGate.Reset();

        /// <summary>Lets <see cref="Put"/> calls blocked by <see cref="PausePuts"/> proceed.</summary>
        public void ResumePuts() => _putGate.Set();

        /// <summary>Returns the items still queued, without removing them.</summary>
        public IMultiRefByteArray[] PeekAll()
        {
            lock (_sync)
            {
                return _items.ToArray();
            }
        }
    }
}
