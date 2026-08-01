using System;
using System.Threading;
using Actuarius.Collections;
using Actuarius.Concurrent;

namespace Pontifex.NoAck.Raw.Unreliable.Direct
{
    internal sealed class SerializedCallbackQueue<T> : IDisposable
    {
        private readonly ConcurrentQueueValve<T> _queue;
        
        private readonly SemaphoreSlim _signal = new(0);
        private int _pendingSignal;
        private readonly Action<T> _handler;
        private volatile bool _disposed;

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

        public bool Post(T state)
        {
            if (_queue.Put(state))
            {
                if (Interlocked.Exchange(ref _pendingSignal, 1) == 0)
                    _signal.Release();
                return true;
            }
            return false;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _queue.CloseValve();
            _signal.Release();
        }

        private void Loop()
        {
            while (!_disposed)
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
                        ExceptionHandler?.Invoke(ex);
                    }
                }
            }

            _signal.Dispose();
        }
    }
}
