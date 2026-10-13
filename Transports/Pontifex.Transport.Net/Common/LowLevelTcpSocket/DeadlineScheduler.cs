using System;
using System.Diagnostics;
using System.Threading;

namespace Pontifex.NetSockets.Tcp
{
    /// <summary>
    /// Internal monotonic clock plus one-shot deadline scheduling seam for the sender shutdown
    /// deadline. The production adapter uses <see cref="Stopwatch"/> timestamps and
    /// <see cref="System.Threading.Timer"/>; tests substitute a manual scheduler that advances time
    /// and releases captured callbacks explicitly. Not public API.
    /// </summary>
    internal interface IDeadlineScheduler
    {
        /// <summary>Monotonic timestamp in scheduler units (never wall-clock).</summary>
        long GetTimestamp();

        /// <summary>Elapsed time since <paramref name="startingTimestamp"/>.</summary>
        TimeSpan GetElapsedTime(long startingTimestamp);

        /// <summary>
        /// Schedules (or replaces) the single pending one-shot callback. Replacement invalidates the
        /// previously scheduled callback at the scheduler level; the caller additionally validates a
        /// generation so an already-dispatched callback cannot act.
        /// </summary>
        void Schedule(TimeSpan dueTime, Action callback);

        /// <summary>Cancels the pending callback if any (idempotent; never invokes it).</summary>
        void Cancel();
    }

    /// <summary>
    /// Production <see cref="IDeadlineScheduler"/>: monotonic <see cref="Stopwatch"/> time and a
    /// single reusable one-shot <see cref="System.Threading.Timer"/>.
    /// </summary>
    internal sealed class SystemDeadlineScheduler : IDeadlineScheduler
    {
        private readonly object _sync = new();
        private Timer? _timer;
        private Action? _callback;

        public long GetTimestamp() => Stopwatch.GetTimestamp();

        public TimeSpan GetElapsedTime(long startingTimestamp)
        {
            long elapsed = Stopwatch.GetTimestamp() - startingTimestamp;
            return TimeSpan.FromSeconds((double)elapsed / Stopwatch.Frequency);
        }

        public void Schedule(TimeSpan dueTime, Action callback)
        {
            lock (_sync)
            {
                _callback = callback;

                if (_timer == null)
                {
                    _timer = new Timer(
                        static state => ((SystemDeadlineScheduler)state!).Fire(),
                        this,
                        dueTime,
                        Timeout.InfiniteTimeSpan);
                }
                else
                {
                    _timer.Change(dueTime, Timeout.InfiniteTimeSpan);
                }
            }
        }

        public void Cancel()
        {
            lock (_sync)
            {
                _callback = null;
                _timer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            }
        }

        private void Fire()
        {
            Action? callback;
            lock (_sync)
            {
                callback = _callback;
                _callback = null;
            }

            callback?.Invoke();
        }
    }
}
