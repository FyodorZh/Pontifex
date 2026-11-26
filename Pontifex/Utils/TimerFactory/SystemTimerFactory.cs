using System;
using System.Threading;

namespace Pontifex.Utils
{
    /// <summary>
    /// Production <see cref="ITimerFactory"/> backed by a one-shot
    /// <see cref="System.Threading.Timer"/>.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Stateless</b>: every scheduled callback owns one timer, and disposing the
    /// returned handle cancels it and releases the timer.</description></item>
    /// <item><description><b>Infinite delay</b>: <see cref="Timeout.InfiniteTimeSpan"/> returns a shared no-op
    /// handle and creates no timer.</description></item>
    /// <item><description><b>Exceptions</b>: a callback that has already started runs to completion; its
    /// exception is swallowed because no logging seam is available at this level and an escaping
    /// exception on the thread pool would terminate the process.</description></item>
    /// </list>
    /// </remarks>
    public sealed class SystemTimerFactory : ITimerFactory
    {
        /// <summary>
        /// The shared instance of the production factory.
        /// </summary>
        public static readonly SystemTimerFactory Instance = new SystemTimerFactory();

        private SystemTimerFactory()
        {
        }

        /// <inheritdoc/>
        public IDisposable Schedule(TimeSpan delay, Action callback)
        {
            if (callback == null!)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            if (delay == Timeout.InfiniteTimeSpan)
            {
                return NoOpHandle.Instance;
            }

            if (delay < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(delay), "The delay must not be negative.");
            }

            return new ScheduledTimer(callback, delay);
        }

        private sealed class NoOpHandle : IDisposable
        {
            public static readonly NoOpHandle Instance = new NoOpHandle();

            public void Dispose()
            {
            }
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
                    // the process. Callers that need failure handling catch inside the callback.
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
