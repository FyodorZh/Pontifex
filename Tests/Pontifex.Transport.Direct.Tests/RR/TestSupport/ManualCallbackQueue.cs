using System;
using System.Collections.Generic;
using System.Linq;
using Pontifex.Utils;

namespace Pontifex.Tests.RR.TestSupport
{
    /// <summary>
    /// Manual serialized callback queue for the RR reliable and RRUnreliable Direct suites.
    /// </summary>
    /// <remarks>
    /// The queue never runs a worker. A test keeps its own reference and calls <see cref="RunAll"/> or
    /// <see cref="RunOne"/> at a deterministic point.
    /// </remarks>
    public sealed class ManualCallbackQueue<T> : IDispatchQueue<T>
    {
        private readonly object _locker = new object();
        private readonly Queue<T> _items = new Queue<T>();
        private readonly Action<T> _disposer;
        private bool _disposed;

        public ManualCallbackQueue(Action<T>? disposer = null)
        {
            _disposer = disposer ?? (_ => { });
        }

        /// <inheritdoc/>
        public event Action<Exception>? ExceptionHandler;

        /// <summary>Gets the number of queued items.</summary>
        public int Count
        {
            get
            {
                lock (_locker)
                {
                    return _items.Count;
                }
            }
        }

        /// <inheritdoc/>
        public bool Post(T state)
        {
            lock (_locker)
            {
                if (_disposed)
                {
                    return false;
                }

                _items.Enqueue(state);
                return true;
            }
        }

        /// <summary>Runs every queued item in FIFO order.</summary>
        /// <param name="handler">The handler to invoke for each item.</param>
        public void RunAll(Action<T> handler)
        {
            T[] snapshot;

            lock (_locker)
            {
                snapshot = _items.ToArray();
                _items.Clear();
            }

            foreach (T item in snapshot)
            {
                try
                {
                    handler(item);
                }
                catch (Exception ex)
                {
                    ExceptionHandler?.Invoke(ex);
                }
            }
        }

        /// <summary>Runs one queued item, if any.</summary>
        /// <param name="handler">The handler to invoke for the item.</param>
        /// <returns><see langword="true"/> when an item ran.</returns>
        public bool RunOne(Action<T> handler)
        {
            T item;

            lock (_locker)
            {
                if (_items.Count == 0)
                {
                    return false;
                }

                item = _items.Dequeue();
            }

            try
            {
                handler(item);
            }
            catch (Exception ex)
            {
                ExceptionHandler?.Invoke(ex);
            }

            return true;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            T[] snapshot;

            lock (_locker)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                snapshot = _items.ToArray();
                _items.Clear();
            }

            foreach (T item in snapshot)
            {
                _disposer(item);
            }
        }
    }
}
