using System;
using System.Collections.Concurrent;
using System.Threading;
using Scriba;

namespace Pontifex.Raw.Unreliable.NoAck
{
    /// <summary>
    /// Serializes execution of queued actions on a dedicated background thread.
    /// Callbacks run strictly one at a time, in FIFO order. Enqueue is safe to
    /// call concurrently from carrier threads. Close flushes queued teardown
    /// actions before returning, but never blocks forever on a hung handler.
    /// </summary>
    internal sealed class SerializedCallbackDispatcher : IDisposable
    {
        private readonly ConcurrentQueue<Action> _queue = new();
        private readonly SemaphoreSlim _signal = new(0);
        private readonly int _capacity;
        private readonly ILogger _logger;
        private int _pendingSignal;
        private volatile bool _closed;
        private Thread? _thread;

        public SerializedCallbackDispatcher(int capacity, string threadName, ILogger logger)
        {
            _capacity = capacity;
            _logger = logger;
            _thread = new Thread(Loop)
            {
                IsBackground = true,
                Name = threadName
            };
            _thread.Start();
        }

        /// <summary>
        /// Posts an action for serialized execution. Returns false when the
        /// dispatcher is closing or its queue is full; the caller must then
        /// perform its own cleanup. A returned true transfers ownership of the
        /// action to the dispatcher.
        /// </summary>
        public bool Enqueue(Action action)
        {
            if (_closed || _queue.Count >= _capacity)
                return false;

            _queue.Enqueue(action);
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

        /// <summary>
        /// Idempotently stops the dispatcher: no new actions are accepted, the
        /// worker is signalled and joined for up to one second, and any actions
        /// that remain queued are drained so teardown work completes.
        /// </summary>
        public void Close()
        {
            if (_closed)
                return;

            _closed = true;
            _signal.Release();

            var thread = _thread;
            if (thread != null && thread == Thread.CurrentThread)
            {
                _thread = null;
                return;
            }

            if (thread != null)
            {
                thread.Join(TimeSpan.FromSeconds(1));
                _thread = null;
            }

            while (_queue.TryDequeue(out var action))
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    _logger.wtf(ex);
                }
            }
        }

        public void Dispose() => Close();

        private void Loop()
        {
            try
            {
                while (true)
                {
                    _signal.Wait();
                    Interlocked.Exchange(ref _pendingSignal, 0);

                    while (_queue.TryDequeue(out var action))
                    {
                        try
                        {
                            action();
                        }
                        catch (Exception ex)
                        {
                            _logger.wtf(ex);
                        }
                    }

                    if (_closed && _queue.IsEmpty)
                        return;
                }
            }
            finally
            {
                _signal.Dispose();
            }
        }
    }
}
