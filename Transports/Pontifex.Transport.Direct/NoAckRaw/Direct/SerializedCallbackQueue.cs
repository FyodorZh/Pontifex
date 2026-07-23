using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Pontifex.NoAck.Raw.Direct
{
    internal sealed class SerializedCallbackQueue : IDisposable
    {
        private readonly BlockingCollection<Action> _queue = new();
        private readonly Thread _thread;
        private bool _disposed;

        public SerializedCallbackQueue(string threadName)
        {
            _thread = new Thread(Loop)
            {
                IsBackground = true,
                Name = threadName
            };
            _thread.Start();
        }

        public void Post(Action callback)
        {
            if (_disposed)
                return;

            try
            {
                _queue.Add(callback);
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
                foreach (var action in _queue.GetConsumingEnumerable())
                {
                    try
                    {
                        action();
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
