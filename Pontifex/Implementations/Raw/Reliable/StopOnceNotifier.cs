using System.Threading;

namespace Pontifex.Raw.Reliable
{
    /// <summary>
    /// One-shot latch that guarantees a handler's terminal stop notification is delivered at most once,
    /// even when several transport paths can observe the same terminal transition.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Thread safety</b>: Safe to call from any thread; the latch is atomic.</description></item>
    /// <item><description><b>Other notes</b>: The instance is shared between the handler gate and every
    /// transport path that may notify the same handler, so the first caller wins and later callers are
    /// silent.</description></item>
    /// </list>
    /// </remarks>
    internal sealed class StopOnceNotifier
    {
        private int _stopped;

        /// <summary>
        /// Claims the single stop notification.
        /// </summary>
        /// <returns><see langword="true"/> for the first caller only; otherwise
        /// <see langword="false"/>.</returns>
        public bool TryNotify()
        {
            return Interlocked.Exchange(ref _stopped, 1) == 0;
        }
    }
}
