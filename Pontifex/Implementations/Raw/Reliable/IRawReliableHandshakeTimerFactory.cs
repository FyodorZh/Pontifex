using System;
using System.Threading;

namespace Pontifex.Raw.Reliable
{
    /// <summary>
    /// Schedules one handshake deadline callback.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Other notes</b>: The seam is public because it appears in a protected constructor of
    /// a public abstract endpoint. A manual implementation is used by tests so the default suite never waits on
    /// a real timer.</description></item>
    /// </list>
    /// </remarks>
    public interface IRawReliableHandshakeTimerFactory
    {
        /// <summary>
        /// Schedules one callback after the given delay.
        /// </summary>
        /// <param name="delay">The delay. <see cref="Timeout.InfiniteTimeSpan"/> disables the deadline.</param>
        /// <param name="callback">The callback to invoke once.</param>
        /// <returns>A handle that cancels the callback when disposed.</returns>
        IDisposable Schedule(TimeSpan delay, Action callback);
    }

    /// <summary>
    /// Default handshake timer factory backed by <see cref="System.Threading.Timer"/>.
    /// </summary>
    internal sealed class SystemRawReliableHandshakeTimerFactory : IRawReliableHandshakeTimerFactory
    {
        public static readonly SystemRawReliableHandshakeTimerFactory Instance =
            new SystemRawReliableHandshakeTimerFactory();

        private sealed class NoOpHandle : IDisposable
        {
            public static readonly NoOpHandle Instance = new NoOpHandle();

            public void Dispose()
            {
            }
        }

        /// <inheritdoc/>
        public IDisposable Schedule(TimeSpan delay, Action callback)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            if (delay == Timeout.InfiniteTimeSpan)
            {
                return NoOpHandle.Instance;
            }

            if (delay < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(delay));
            }

            return new Timer(_ => callback(), null, delay, Timeout.InfiniteTimeSpan);
        }
    }
}
