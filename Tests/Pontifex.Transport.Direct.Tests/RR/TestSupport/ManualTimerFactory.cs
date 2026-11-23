using System;
using System.Collections.Generic;
using System.Linq;
using Pontifex.RR.Reliable;

namespace Pontifex.Tests.RR.TestSupport
{
    /// <summary>
    /// Deterministic <see cref="IRRReliableTimerFactory"/> for the RR reliable suites.
    /// </summary>
    /// <remarks>
    /// The factory never fires on its own. A test inspects <see cref="PendingCount"/> to prove that a
    /// callback was scheduled before <c>onStopped</c>, then explicitly fires, suppresses, or disposes
    /// every handle. The factory is shared loose test support because the helpers, abstract
    /// transport, and Direct suites live in one test project.
    /// </remarks>
    public sealed class ManualTimerFactory : IRRReliableTimerFactory
    {
        private readonly object _locker = new object();
        private readonly List<ManualTimerHandle> _handles = new List<ManualTimerHandle>();

        /// <summary>Gets the number of callbacks scheduled over the factory lifetime.</summary>
        public int ScheduledCount
        {
            get
            {
                lock (_locker)
                {
                    return _handles.Count;
                }
            }
        }

        /// <summary>Gets the number of scheduled callbacks that were neither fired, suppressed, nor
        /// disposed.</summary>
        public int PendingCount
        {
            get
            {
                lock (_locker)
                {
                    return _handles.Count(handle => !handle.IsClosed);
                }
            }
        }

        /// <summary>Gets a snapshot of every handle scheduled so far.</summary>
        public IReadOnlyList<ManualTimerHandle> Handles
        {
            get
            {
                lock (_locker)
                {
                    return _handles.ToArray();
                }
            }
        }

        /// <inheritdoc/>
        public IDisposable Schedule(TimeSpan delay, Action callback)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            if (delay < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(delay));
            }

            var handle = new ManualTimerHandle(delay, callback);

            lock (_locker)
            {
                _handles.Add(handle);
            }

            return handle;
        }

        /// <summary>
        /// Fires the handle at <paramref name="index"/>.
        /// </summary>
        /// <returns><see langword="true"/> when the callback ran.</returns>
        public bool Fire(int index)
        {
            return Handles[index].TryFire();
        }

        /// <summary>
        /// Fires every pending handle.
        /// </summary>
        /// <returns>The number of callbacks that ran.</returns>
        public int FireAll()
        {
            int fired = 0;

            foreach (ManualTimerHandle handle in Handles)
            {
                if (handle.TryFire())
                {
                    fired++;
                }
            }

            return fired;
        }

        /// <summary>
        /// Suppresses every pending handle without running its callback.
        /// </summary>
        public void SuppressAll()
        {
            foreach (ManualTimerHandle handle in Handles)
            {
                handle.Suppress();
            }
        }
    }

    /// <summary>
    /// One handle scheduled by <see cref="ManualTimerFactory"/>.
    /// </summary>
    public sealed class ManualTimerHandle : IDisposable
    {
        private readonly object _locker = new object();
        private readonly Action _callback;
        private bool _closed;
        private bool _fired;

        internal ManualTimerHandle(TimeSpan delay, Action callback)
        {
            Delay = delay;
            _callback = callback;
        }

        /// <summary>Gets the requested delay.</summary>
        public TimeSpan Delay { get; }

        /// <summary>Gets whether the callback ran.</summary>
        public bool IsFired
        {
            get
            {
                lock (_locker)
                {
                    return _fired;
                }
            }
        }

        /// <summary>Gets whether the handle was fired, suppressed, or disposed.</summary>
        public bool IsClosed
        {
            get
            {
                lock (_locker)
                {
                    return _closed;
                }
            }
        }

        /// <summary>
        /// Runs the callback once. The callback runs outside the handle lock, so it may schedule
        /// another timer or dispose this handle.
        /// </summary>
        /// <returns><see langword="true"/> when the callback ran.</returns>
        public bool TryFire()
        {
            lock (_locker)
            {
                if (_closed)
                {
                    return false;
                }

                _closed = true;
                _fired = true;
            }

            _callback();
            return true;
        }

        /// <summary>
        /// Closes the handle without running the callback.
        /// </summary>
        public void Suppress()
        {
            lock (_locker)
            {
                _closed = true;
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Suppress();
        }
    }
}
