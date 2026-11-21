using System;
using System.Collections.Generic;
using Operarius;

namespace Pontifex.RR.Unreliable
{
    /// <summary>
    /// Table of the outstanding requests of one RR unreliable endpoint. An entry is removed when it is
    /// taken, when it expires, or when the table is cleared.
    /// </summary>
    /// <remarks>
    /// Entries are pruned opportunistically on every operation. Expiry order follows insertion order
    /// because every entry uses the same TTL and request ids are monotonic.
    /// </remarks>
    internal sealed class RRUnreliableRequestTable<TValue>
    {
        private readonly struct Entry
        {
            public Entry(TValue value, DateTime expiresAt)
            {
                Value = value;
                ExpiresAt = expiresAt;
            }

            public TValue Value { get; }

            public DateTime ExpiresAt { get; }
        }

        private readonly object _locker = new object();
        private readonly Dictionary<long, Entry> _entries = new Dictionary<long, Entry>();
        private readonly Queue<long> _expiryOrder = new Queue<long>();
        private readonly IDateTimeProvider _timeProvider;
        private readonly TimeSpan _ttl;

        public RRUnreliableRequestTable(IDateTimeProvider timeProvider, TimeSpan ttl)
        {
            _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

            if (ttl <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(ttl), "The TTL must be positive.");
            }

            _ttl = ttl;
        }

        public void Add(long requestId, TValue value)
        {
            lock (_locker)
            {
                DateTime now = _timeProvider.Now;
                Sweep(now);
                _entries.Add(requestId, new Entry(value, now + _ttl));
                _expiryOrder.Enqueue(requestId);
            }
        }

        public bool TryTake(long requestId, out TValue value)
        {
            lock (_locker)
            {
                Sweep(_timeProvider.Now);

                if (_entries.TryGetValue(requestId, out Entry entry))
                {
                    _entries.Remove(requestId);
                    value = entry.Value;
                    return true;
                }

                value = default!;
                return false;
            }
        }

        public void Clear()
        {
            lock (_locker)
            {
                _entries.Clear();
                _expiryOrder.Clear();
            }
        }

        private void Sweep(DateTime now)
        {
            while (_expiryOrder.Count > 0)
            {
                long requestId = _expiryOrder.Peek();

                if (!_entries.TryGetValue(requestId, out Entry entry))
                {
                    // Already removed by TryTake.
                    _expiryOrder.Dequeue();
                    continue;
                }

                if (entry.ExpiresAt > now)
                {
                    return;
                }

                _expiryOrder.Dequeue();
                _entries.Remove(requestId);
            }
        }
    }
}
