using System;
using System.Threading;

namespace Pontifex.RR.Reliable
{
    /// <summary>
    /// Production <see cref="IRRReliableTimerFactory"/> backed by a one-shot
    /// <see cref="System.Threading.Timer"/>.
    /// </summary>
    /// <remarks>
    /// The factory is stateless. Every scheduled callback owns one timer, and disposing the returned
    /// handle cancels it and releases the timer. A callback that has already started runs to
    /// completion; its exception is swallowed because no logging seam is available at this level.
    /// </remarks>
    internal sealed class SystemRRReliableTimerFactory : IRRReliableTimerFactory
    {
        /// <summary>
        /// The shared instance of the production factory.
        /// </summary>
        public static readonly SystemRRReliableTimerFactory Instance = new SystemRRReliableTimerFactory();

        /// <inheritdoc/>
        public IDisposable Schedule(TimeSpan delay, Action callback)
        {
            if (callback == null!)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            if (delay < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(delay), "The delay must not be negative.");
            }

            return new ScheduledTimer(callback, delay);
        }

        private sealed class ScheduledTimer : IDisposable
        {
            private readonly Action _callback;
            private readonly Timer _timer;
            private int _closed;

            public ScheduledTimer(Action callback, TimeSpan delay)
            {
                _callback = callback;
                _timer = new Timer(static state => ((ScheduledTimer)state!).Fire(), this,
                    Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

                try
                {
                    _timer.Change(delay, Timeout.InfiniteTimeSpan);
                }
                catch
                {
                    _timer.Dispose();
                    throw;
                }
            }

            private void Fire()
            {
                if (Interlocked.Exchange(ref _closed, 1) != 0)
                {
                    return;
                }

                try
                {
                    _callback();
                }
                catch (Exception)
                {
                    // A TTL callback runs on the thread pool; an escaping exception would terminate
                    // the process. The core callbacks log their own failures.
                }
                finally
                {
                    _timer.Dispose();
                }
            }

            public void Dispose()
            {
                if (Interlocked.Exchange(ref _closed, 1) == 0)
                {
                    _timer.Dispose();
                }
            }
        }
    }
}
