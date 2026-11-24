using System;
using System.Threading;
using Actuarius.Collections;
using Actuarius.Concurrent;

namespace Pontifex.Utils
{
    /// <summary>
    /// Serializes execution of queued callbacks on a dedicated background thread.
    /// Callbacks run strictly one at a time, in FIFO order. Post is safe to call
    /// concurrently from any thread.
    /// </summary>
    public interface ISerializedCallbackQueue<in T> : IDisposable
    {
        bool Post(T state);
    }

    /// <inheritdoc />
    public sealed class SerializedCallbackQueue<T> : IDispatchQueue<T>
    {
        private readonly ConcurrentQueueValve<T> _queue;
        
        private readonly SemaphoreSlim _signal = new(0);
        private int _pendingSignal;
        private readonly Action<T> _handler;
        private int _disposed;

        public event Action<Exception>? ExceptionHandler;

        public SerializedCallbackQueue(int capacity, string threadName, Action<T> handler, Action<T> disposer)
        {
            _queue = new (
                new LimitedConcurrentQueue<T>(capacity), disposer, _ => { });
            
            _handler = handler;
            var thread = new Thread(Loop)
            {
                IsBackground = true,
                Name = threadName
            };
            thread.Start();
        }

        /// <summary>
        /// Initializes an unbounded serialized callback queue.
        /// </summary>
        /// <param name="threadName">The name of the worker thread.</param>
        /// <param name="handler">The callback invoked for each queued item.</param>
        /// <param name="disposer">The callback invoked for an item that is still queued when the queue is
        /// disposed.</param>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Other notes</b>: This overload uses a genuinely unbounded concurrent queue
        /// instead of a very large pre-sized array. It is intended for a mode that promises not to drop an
        /// accepted message, for example reliable debug mode.</description></item>
        /// </list>
        /// </remarks>
        public SerializedCallbackQueue(string threadName, Action<T> handler, Action<T> disposer)
        {
            _queue = new (
                new SystemConcurrentQueue<T>(), disposer, _ => { });

            _handler = handler;
            var thread = new Thread(Loop)
            {
                IsBackground = true,
                Name = threadName
            };
            thread.Start();
        }

        /// <summary>
        /// Queues <paramref name="state"/> for serialized execution on the worker thread.
        /// Safe to call concurrently from any thread.
        /// </summary>
        /// <returns>
        /// <c>true</c> when ownership of <paramref name="state"/> is transferred to the queue:
        /// the item will be passed to the handler, or released via the disposer if it is still
        /// queued when the queue is disposed. <c>false</c> when the queue is full or has already
        /// been disposed; <paramref name="state"/> is left untouched and the caller must release it.
        /// </returns>
        public bool Post(T state)
        {
            if (_queue.EnqueueEx(state) == ValveEnqueueResult.Ok)
            {
                if (Interlocked.Exchange(ref _pendingSignal, 1) == 0)
                {
                    try
                    {
                        _signal.Release();
                    }
                    catch (ObjectDisposedException)
                    {
                    }
                }
                return true;
            }
            return false;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            _queue.CloseValve();
            try
            {
                _signal.Release();
            }
            catch (ObjectDisposedException)
            {
            }
        }

        private void Loop()
        {
            try
            {
                while (Volatile.Read(ref _disposed) == 0)
                {
                    _signal.Wait();
                    Interlocked.Exchange(ref _pendingSignal, 0);

                    while (_queue.TryPop(out var element))
                    {
                        try
                        {
                            _handler(element);
                        }
                        catch (Exception ex)
                        {
                            try
                            {
                                ExceptionHandler?.Invoke(ex);
                            }
                            catch
                            {
                            }
                        }
                    }
                }
            }
            finally
            {
                _signal.Dispose();
            }
        }
    }
}
