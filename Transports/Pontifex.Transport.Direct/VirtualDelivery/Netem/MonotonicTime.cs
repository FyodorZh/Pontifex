using System;
using System.Diagnostics;

namespace Pontifex.VirtualDelivery.Netem
{
    /// <summary>
    /// Overflow-free monotonic nanosecond conversion shared by the netem components.
    /// </summary>
    public static class MonotonicTime
    {
        /// <summary>
        /// Converts a tick count to nanoseconds using split arithmetic that cannot overflow.
        /// </summary>
        /// <param name="ticks">The tick count.</param>
        /// <param name="frequency">The ticks per second.</param>
        /// <returns>The equivalent nanosecond count.</returns>
        public static long ToNanoseconds(long ticks, long frequency)
        {
            if (frequency <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(frequency));
            }

            long seconds = ticks / frequency;
            long remainder = ticks % frequency;
            return seconds * 1_000_000_000L + remainder * 1_000_000_000L / frequency;
        }

        /// <summary>
        /// Gets the current monotonic time in nanoseconds from <see cref="Stopwatch"/>.
        /// </summary>
        public static long NowNanoseconds()
        {
            return ToNanoseconds(Stopwatch.GetTimestamp(), Stopwatch.Frequency);
        }

        /// <summary>
        /// Creates a clock function backed by <see cref="Stopwatch"/>, or returns the supplied test clock.
        /// </summary>
        /// <param name="clock">The override, or <see langword="null"/> for the real monotonic source.</param>
        public static Func<long> CreateClock(Func<long>? clock = null)
        {
            return clock ?? NowNanoseconds;
        }
    }
}
