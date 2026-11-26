using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Pontifex.Utils
{
    /// <summary>
    /// Deterministic <see cref="ITimerFactory"/> with a caller-driven virtual clock.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Purpose</b>: a caller arms callbacks through the normal contract and then
    /// explicitly fires a handle or advances <see cref="Now"/> with <see cref="Advance"/>. It lets tests
    /// and deterministic harnesses prove that work was scheduled before a lifecycle event without waiting
    /// on real time.</description></item>
    /// <item><description><b>Infinite delay</b>: <see cref="Timeout.InfiniteTimeSpan"/> is accepted and
    /// recorded like any other delay, but the handle never becomes due, so <see cref="Advance"/> never
    /// fires it. An explicit <see cref="ManualTimerHandle.TryFire"/> still runs the callback.</description></item>
    /// <item><description><b>Retention</b>: every scheduled handle stays tracked until
    /// <see cref="Clear"/> is called, so indices returned by <see cref="Handles"/> are stable for the
    /// lifetime of the factory. A long-lived factory therefore grows without bound; call
    /// <see cref="Clear"/> between scenarios.</description></item>
    /// <item><description><b>Threading</b>: scheduling, advancing, firing, suppressing, and clearing are
    /// serialized by one internal lock. A callback runs outside that lock, so it may schedule another
    /// timer, advance the clock, or dispose its own handle.</description></item>
    /// </list>
    /// </remarks>
    public sealed class ManualTimerFactory : ITimerFactory
    {
        private readonly object _locker = new object();
        private readonly List<ManualTimerHandle> _handles = new List<ManualTimerHandle>();
        private DateTime _now = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        /// <summary>Gets the current virtual time.</summary>
        /// <remarks>
        /// Time moves only through <see cref="Advance"/>. The initial value is fixed so a harness can pass
        /// <see cref="Now"/> to code that needs a timestamp without depending on the wall clock.
        /// </remarks>
        public DateTime Now
        {
            get
            {
                lock (_locker)
                {
                    return _now;
                }
            }
        }

        /// <summary>Gets the number of callbacks scheduled since the last <see cref="Clear"/>.</summary>
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

        /// <summary>Gets a snapshot of every handle scheduled since the last <see cref="Clear"/>.</summary>
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

        /// <summary>Gets the scheduled delays of every pending handle in due-time order.</summary>
        public IReadOnlyList<TimeSpan> PendingDelays
        {
            get
            {
                lock (_locker)
                {
                    return _handles
                        .Where(handle => !handle.IsClosed)
                        .OrderBy(handle => handle.Due)
                        .Select(handle => handle.Delay)
                        .ToList();
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

            if (delay < TimeSpan.Zero && delay != Timeout.InfiniteTimeSpan)
            {
                throw new ArgumentOutOfRangeException(nameof(delay));
            }

            lock (_locker)
            {
                DateTime due = delay == Timeout.InfiniteTimeSpan
                    ? DateTime.MaxValue
                    : _now + delay;
                var handle = new ManualTimerHandle(delay, due, callback);
                _handles.Add(handle);
                return handle;
            }
        }

        /// <summary>
        /// Advances the virtual clock and fires every handle that becomes due, in due-time order.
        /// </summary>
        /// <remarks>
        /// A callback may schedule another timer that is already due at the new time; it fires in the same
        /// call. A callback exception stops the advance and propagates to the caller; the handle that threw
        /// stays closed. Infinite handles never become due.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="by"/> is negative.</exception>
        public void Advance(TimeSpan by)
        {
            if (by < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(by));
            }

            lock (_locker)
            {
                _now += by;
            }

            while (true)
            {
                ManualTimerHandle? next;
                lock (_locker)
                {
                    next = _handles
                        .Where(handle => !handle.IsClosed && handle.Due <= _now)
                        .OrderBy(handle => handle.Due)
                        .FirstOrDefault();
                }

                if (next == null)
                {
                    return;
                }

                next.TryFire();
            }
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

        /// <summary>
        /// Suppresses every pending handle and forgets all tracked handles.
        /// </summary>
        /// <remarks>
        /// <see cref="ScheduledCount"/>, <see cref="PendingCount"/>, <see cref="PendingDelays"/>, and
        /// <see cref="Handles"/> start fresh after the call. A handle that the caller still references keeps
        /// its own state; it is simply no longer tracked by the factory. <see cref="Now"/> is not reset.
        /// </remarks>
        public void Clear()
        {
            lock (_locker)
            {
                foreach (ManualTimerHandle handle in _handles)
                {
                    handle.Suppress();
                }

                _handles.Clear();
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

        internal ManualTimerHandle(TimeSpan delay, DateTime due, Action callback)
        {
            Delay = delay;
            Due = due;
            _callback = callback;
        }

        /// <summary>Gets the requested delay, including <see cref="Timeout.InfiniteTimeSpan"/>.</summary>
        public TimeSpan Delay { get; }

        /// <summary>Gets the virtual time at which the handle becomes due.</summary>
        /// <remarks><see cref="DateTime.MaxValue"/> for an infinite delay.</remarks>
        public DateTime Due { get; }

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
        /// Runs the callback once. The callback runs outside the handle lock, so it may schedule another
        /// timer or dispose this handle.
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
