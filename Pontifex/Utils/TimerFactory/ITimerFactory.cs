using System;
using System.Threading;

namespace Pontifex.Utils
{
    /// <summary>
    /// Schedules one-shot callbacks after a delay.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>One shot</b>: each call arms one callback. A callback runs at most once and is
    /// never rescheduled by the factory.</description></item>
    /// <item><description><b>Delays</b>: <see cref="Timeout.InfiniteTimeSpan"/> arms a
    /// callback that never fires; the returned handle still cancels safely. <see cref="TimeSpan.Zero"/>
    /// fires as soon as the implementation can deliver it. Any other negative delay is invalid.</description></item>
    /// <item><description><b>Cancellation</b>: disposing the returned handle cancels the callback when it has
    /// not started yet. Disposing an already-fired or already-disposed handle is safe.</description></item>
    /// <item><description><b>Threading</b>: a production implementation delivers callbacks on a thread-pool
    /// thread, so the callback must not assume a caller thread. The handle may be disposed from any
    /// thread.</description></item>
    /// <item><description><b>Exceptions</b>: a callback that throws does not escape the factory, because an
    /// escaping exception on the thread pool would terminate the process. Callers that need failure
    /// handling must catch inside the callback.</description></item>
    /// </list>
    /// </remarks>
    public interface ITimerFactory
    {
        /// <summary>
        /// Schedules <paramref name="callback"/> to run once after <paramref name="delay"/>.
        /// </summary>
        /// <param name="delay">The delay before the callback runs. Must be non-negative or
        /// <see cref="Timeout.InfiniteTimeSpan"/>.</param>
        /// <param name="callback">The callback to run. Must not be <see langword="null"/>.</param>
        /// <returns>A handle that cancels the scheduled callback when disposed.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="callback"/> is
        /// <see langword="null"/>.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="delay"/> is negative and is not
        /// <see cref="Timeout.InfiniteTimeSpan"/>.</exception>
        IDisposable Schedule(TimeSpan delay, Action callback);
    }
}
