using System;

namespace Pontifex.RR.Reliable
{
    /// <summary>
    /// Schedules the one-shot TTL callbacks of an RR reliable transport.
    /// </summary>
    /// <remarks>
    /// The reliable core has no clock: every request TTL, response TTL, and terminal deferral is
    /// driven by a scheduled callback from this factory. A carrier or a test may supply its own
    /// deterministic implementation through the protected constructors of the core types; when none
    /// is supplied, the core uses a system timer.
    /// </remarks>
    public interface IRRReliableTimerFactory
    {
        /// <summary>
        /// Schedules <paramref name="callback"/> to run once after <paramref name="delay"/>.
        /// </summary>
        /// <param name="delay">The delay before the callback runs. Must not be negative.</param>
        /// <param name="callback">The callback to run. Must not be <see langword="null"/>.</param>
        /// <returns>A handle that cancels the scheduled callback when disposed. Disposing an
        /// already-fired or already-disposed handle is safe.</returns>
        IDisposable Schedule(TimeSpan delay, Action callback);
    }
}
