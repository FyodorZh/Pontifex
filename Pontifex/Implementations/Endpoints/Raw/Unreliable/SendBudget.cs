using System;
using System.Threading;

namespace Pontifex.Raw.Unreliable
{
    /// <summary>
    /// Best-effort outbound admission budget, in bytes and messages, shared by all sessions of one unreliable
    /// server transport.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Thread safety</b>: All members are safe to call concurrently from any thread.
    /// The counters are updated with <see cref="Interlocked"/>, and <see cref="CanAccept"/> reads a
    /// race-tolerant snapshot.</description></item>
    /// <item><description><b>Preconditions/Postconditions</b>: The limits must not be negative. The budget
    /// does not validate that an accepted reservation is later released; the owner drives the matching
    /// negative deltas through <see cref="Apply"/>.</description></item>
    /// <item><description><b>Other notes</b>: The check is deliberately best effort: concurrent senders may
    /// overshoot a bounded amount, which is accepted for unreliable mode. No member throws after
    /// construction.</description></item>
    /// </list>
    /// </remarks>
    public sealed class SendBudget
    {
        private readonly long _maxBytes;
        private readonly int _maxMessages;

        private long _pendingBytes;
        private int _pendingMessages;

        /// <summary>
        /// Initializes a new budget with the given byte and message limits.
        /// </summary>
        /// <param name="maxBytes">The inclusive maximum number of reserved payload bytes; must not be
        /// negative. Zero accepts no non-empty message.</param>
        /// <param name="maxMessages">The inclusive maximum number of reserved messages; must not be negative.
        /// Zero accepts no message.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxBytes"/> or
        /// <paramref name="maxMessages"/> is negative.</exception>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Preconditions/Postconditions</b>: Both limits must be non-negative.
        /// <paramref name="maxBytes"/> must be large enough for the largest message the transport admits, or
        /// every send is rejected.</description></item>
        /// <item><description><b>Other notes</b>: The budget starts empty. The limits are immutable.</description></item>
        /// </list>
        /// </remarks>
        public SendBudget(long maxBytes, int maxMessages)
        {
            if (maxBytes < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxBytes));
            }

            if (maxMessages < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxMessages));
            }

            _maxBytes = maxBytes;
            _maxMessages = maxMessages;
        }

        /// <summary>
        /// Determines whether one more message of the given payload size would fit within the limits.
        /// </summary>
        /// <param name="size">The payload size, in bytes, of the message to admit; must not be
        /// negative.</param>
        /// <returns><see langword="true"/> when the current reservation plus the message is within both limits;
        /// otherwise <see langword="false"/>. A negative size is always rejected.</returns>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Safe to call concurrently; the result is a best-effort
        /// snapshot and may be stale by the time the caller reserves.</description></item>
        /// <item><description><b>Other notes</b>: The check reserves nothing; the caller must
        /// <see cref="Apply"/> the reservation. It never throws.</description></item>
        /// </list>
        /// </remarks>
        public bool CanAccept(int size)
        {
            if (size < 0)
            {
                return false;
            }

            long pendingBytes = Interlocked.Read(ref _pendingBytes);
            int pendingMessages = Volatile.Read(ref _pendingMessages);

            return pendingBytes + size <= _maxBytes && pendingMessages + 1 <= _maxMessages;
        }

        /// <summary>
        /// Applies a signed change to the reserved byte and message counters.
        /// </summary>
        /// <param name="deltaBytes">The signed change of the reserved payload bytes; positive on reservation,
        /// negative on release.</param>
        /// <param name="deltaMessages">The signed change of the reserved message count; positive on
        /// reservation, negative on release.</param>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety</b>: Safe to call concurrently; each counter is aggregated
        /// atomically.</description></item>
        /// <item><description><b>Other notes</b>: The two counters are updated independently, so a concurrent
        /// reader may briefly observe a mixed snapshot. The member never throws and does not validate that the
        /// counters stay non-negative; the owner must pair every reservation with exactly one
        /// release.</description></item>
        /// </list>
        /// </remarks>
        public void Apply(int deltaBytes, int deltaMessages)
        {
            Interlocked.Add(ref _pendingBytes, deltaBytes);
            Interlocked.Add(ref _pendingMessages, deltaMessages);
        }
    }
}
