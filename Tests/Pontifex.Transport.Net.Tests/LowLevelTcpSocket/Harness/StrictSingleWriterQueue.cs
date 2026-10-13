using Actuarius.Collections;
using Actuarius.Memory;

namespace Pontifex.Tests.NetSockets.LowLevelTcpSocketTests.Harness
{
    /// <summary>
    /// A strict single-reader/single-writer queue double: it rejects a second concurrent
    /// reader or writer instead of silently serializing the illegal access away like a lock-based
    /// queue would. A valid single-writer producer works normally; concurrent producer publication
    /// throws <see cref="InvalidOperationException"/> and is recorded in
    /// <see cref="WriterViolations"/>.
    /// </summary>
    /// <remarks>
    /// The real source is an SPSC queue whose internals do not serialize multiple readers or
    /// writers. This double makes the integration contract executable: the transport must remain the
    /// sole reader, and application-level publication must be serialized by one writer (or an
    /// adapter). It is deliberately not an MPSC queue.
    /// </remarks>
    internal sealed class StrictSingleWriterQueue : ISingleReaderWriterConcurrentQueue<IMultiRefByteArray>
    {
        private readonly object _sync = new();
        private readonly Queue<IMultiRefByteArray> _items = new();
        private int _activeReaders;
        private int _activeWriters;
        private int _readerViolations;
        private int _writerViolations;

        /// <summary>Invoked inside <see cref="Put"/> before the value is enqueued; may block.</summary>
        public Action? OnWriteEntered { get; set; }

        /// <summary>Number of rejected concurrent <see cref="Put"/> calls.</summary>
        public int WriterViolations => Volatile.Read(ref _writerViolations);

        /// <summary>Number of rejected concurrent <see cref="TryPop"/> calls.</summary>
        public int ReaderViolations => Volatile.Read(ref _readerViolations);

        public int Count
        {
            get { lock (_sync) { return _items.Count; } }
        }

        public bool Put(IMultiRefByteArray value)
        {
            if (Interlocked.Increment(ref _activeWriters) > 1)
            {
                Interlocked.Increment(ref _writerViolations);
                Interlocked.Decrement(ref _activeWriters);
                throw new InvalidOperationException(
                    "SPSC violation: this source is single-writer; concurrent producer publication is not supported.");
            }

            try
            {
                OnWriteEntered?.Invoke();
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
            if (Interlocked.Increment(ref _activeReaders) > 1)
            {
                Interlocked.Increment(ref _readerViolations);
                Interlocked.Decrement(ref _activeReaders);
                throw new InvalidOperationException(
                    "SPSC violation: this source is single-reader; concurrent reads are not supported.");
            }

            try
            {
                lock (_sync)
                {
                    if (_items.Count > 0)
                    {
                        value = _items.Dequeue();
                        return true;
                    }
                }

                value = null!;
                return false;
            }
            finally
            {
                Interlocked.Decrement(ref _activeReaders);
            }
        }
    }
}
