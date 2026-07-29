using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Pontifex.NoAck.Raw.Direct
{
    internal sealed class SerializedCallbackQueue<T> : IDisposable
    {
        private readonly BlockingCollection<T> _queue = new();
        private readonly Thread _thread;
        private readonly Action<T> _handler;
        private bool _disposed;

        public SerializedCallbackQueue(string threadName, Action<T> handler)
        {
            _handler = handler;
            _thread = new Thread(Loop)
            {
                IsBackground = true,
                Name = threadName
            };
            _thread.Start();
        }

        public void Post(T state)
        {
            if (_disposed)
                return;

            try
            {
                _queue.Add(state);
            }
            catch (ObjectDisposedException)
            {
            }
            catch (InvalidOperationException)
            {
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _queue.CompleteAdding();
            if (_thread.IsAlive)
                _thread.Join(TimeSpan.FromSeconds(5));
            _queue.Dispose();
        }

        private void Loop()
        {
            try
            {
                foreach (var state in _queue.GetConsumingEnumerable())
                {
                    try
                    {
                        _handler(state);
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }
        }
    }
}
