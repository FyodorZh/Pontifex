using System;
using System.Collections.Generic;
using System.Threading;

namespace Pontifex.RR.Reliable
{
    /// <summary>
    /// Unbounded serialized callback pump of one reliable RR client.
    /// </summary>
    /// <remarks>
    /// Every client callback of one transport runs on the single pump thread, in post order, so a
    /// per-request terminal scheduled from inside a callback is deferred until that callback
    /// returns. The pump is lazy: it starts on the first post, drains the queue, and stops its
    /// thread when no work and no scheduled terminal remain. A terminal is counted from the moment
    /// it is posted until its action completes, which lets a test observe the scheduled work from
    /// inside <c>onStopped</c>.
    /// </remarks>
    internal sealed class RRReliableClientCallbackPump
    {
        private readonly object _locker = new object();
        private readonly Queue<Action> _queue = new Queue<Action>();
        private readonly Action<Exception> _onError;
        private readonly string _threadName;

        private int _terminalsScheduled;
        private bool _workerRunning;
        private bool _disposed;

        /// <summary>
        /// Initializes a pump.
        /// </summary>
        /// <param name="threadName">The name of the worker thread.</param>
        /// <param name="onError">The sink of an exception escaping a posted callback.</param>
        public RRReliableClientCallbackPump(string threadName, Action<Exception> onError)
        {
            _threadName = threadName ?? throw new ArgumentNullException(nameof(threadName));
            _onError = onError ?? throw new ArgumentNullException(nameof(onError));
        }

        /// <summary>
        /// Gets or sets the test gate invoked before every queued callback. A test can block the
        /// pump, inspect the scheduled terminal count, and then release it.
        /// </summary>
        internal Action? GateForTests { get; set; }

        /// <summary>
        /// Gets the number of terminal actions posted but not yet completed.
        /// </summary>
        internal int ScheduledTerminalCount => Volatile.Read(ref _terminalsScheduled);

        /// <summary>
        /// Posts one callback.
        /// </summary>
        /// <param name="callback">The callback to run on the pump thread.</param>
        /// <param name="terminal">Whether the callback is a terminal notification.</param>
        /// <returns><see langword="false"/> when the pump was disposed; the caller keeps
        /// ownership of any state the callback would have consumed.</returns>
        internal bool Post(Action callback, bool terminal)
        {
            if (callback == null!)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            lock (_locker)
            {
                if (_disposed)
                {
                    return false;
                }

                _queue.Enqueue(callback);

                if (terminal)
                {
                    _terminalsScheduled++;
                }

                if (!_workerRunning)
                {
                    _workerRunning = true;
                    var thread = new Thread(Run)
                    {
                        IsBackground = true,
                        Name = _threadName
                    };
                    thread.Start();
                }

                return true;
            }
        }

        /// <summary>
        /// Reports that one posted terminal action completed.
        /// </summary>
        internal void TerminalCompleted()
        {
            lock (_locker)
            {
                if (_terminalsScheduled > 0)
                {
                    _terminalsScheduled--;
                }
            }
        }

        /// <summary>
        /// Stops accepting new work. Already posted work still drains, including every scheduled
        /// terminal, and the worker thread then stops itself.
        /// </summary>
        internal void Dispose()
        {
            lock (_locker)
            {
                _disposed = true;
                Monitor.PulseAll(_locker);
            }
        }

        private void Run()
        {
            while (true)
            {
                Action action;

                lock (_locker)
                {
                    if (_queue.Count == 0)
                    {
                        _workerRunning = false;
                        return;
                    }

                    action = _queue.Dequeue();
                }

                Action? gate = GateForTests;

                if (gate != null)
                {
                    try
                    {
                        gate();
                    }
                    catch (Exception ex)
                    {
                        _onError(ex);
                    }
                }

                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    _onError(ex);
                }
            }
        }
    }
}
