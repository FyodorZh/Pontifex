using System;
using System.Collections.Generic;
using Operarius;

namespace Pontifex.RR.Unreliable
{
    /// <summary>
    /// Table of the outstanding requests of one RR unreliable client. An entry is removed when it is
    /// taken, when it expires, or when the table is cleared.
    /// </summary>
    /// <remarks>
    /// Expired entries are pruned opportunistically when a new entry is added. The deadline of an
    /// entry may be re-armed, so expiry is checked per entry rather than by insertion order.
    /// </remarks>
    internal sealed class RRUnreliableRequestTable<TValue>
        where TValue : class
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
        private readonly Dictionary<ushort, Entry> _entries = new Dictionary<ushort, Entry>();
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

        /// <summary>
        /// Gets the number of entries, including expired entries that were not swept yet.
        /// </summary>
        public int Count
        {
            get
            {
                lock (_locker)
                {
                    return _entries.Count;
                }
            }
        }

        /// <summary>
        /// Adds an entry with a provisional deadline and prunes expired entries. The deadline is set
        /// by <see cref="ReArm"/> once the frame is submitted.
        /// </summary>
        /// <param name="requestId">The id of the entry.</param>
        /// <param name="value">The value to store.</param>
        /// <param name="onExpired">Invoked for every value removed while pruning expired
        /// entries.</param>
        /// <returns><see langword="true"/> when the entry was added; <see langword="false"/> when the
        /// id is already present.</returns>
        public bool TryAdd(ushort requestId, TValue value, Action<TValue>? onExpired = null,
            bool sweep = true)
        {
            lock (_locker)
            {
                if (sweep)
                {
                    Sweep(_timeProvider.Now, onExpired);
                }

                return _entries.TryAdd(requestId, new Entry(value, DateTime.MaxValue));
            }
        }

        /// <summary>
        /// Restarts the TTL of an existing entry. Does nothing when the entry is already gone.
        /// </summary>
        public void ReArm(ushort requestId)
        {
            lock (_locker)
            {
                if (_entries.TryGetValue(requestId, out Entry entry))
                {
                    _entries[requestId] = new Entry(entry.Value, _timeProvider.Now + _ttl);
                }
            }
        }

        /// <summary>
        /// Gets the entry with the given id without removing it.
        /// </summary>
        /// <param name="requestId">The id of the entry.</param>
        /// <param name="value">The value of the entry, or the default when it is absent.</param>
        /// <param name="expired">Whether the entry had already expired.</param>
        /// <returns><see langword="true"/> when the entry is present; otherwise
        /// <see langword="false"/>.</returns>
        public bool TryGet(ushort requestId, out TValue value, out bool expired)
        {
            lock (_locker)
            {
                if (_entries.TryGetValue(requestId, out Entry entry))
                {
                    value = entry.Value;
                    expired = entry.ExpiresAt <= _timeProvider.Now;
                    return true;
                }

                value = default!;
                expired = false;
                return false;
            }
        }

        /// <summary>
        /// Removes the entry with the given id.
        /// </summary>
        /// <param name="requestId">The id of the entry.</param>
        /// <param name="value">The removed value, or the default when the entry was not present.</param>
        /// <param name="expired">Whether the removed entry had already expired.</param>
        /// <returns><see langword="true"/> when an entry was removed; otherwise
        /// <see langword="false"/>.</returns>
        public bool TryTake(ushort requestId, out TValue value, out bool expired)
        {
            lock (_locker)
            {
                if (_entries.TryGetValue(requestId, out Entry entry))
                {
                    _entries.Remove(requestId);
                    expired = entry.ExpiresAt <= _timeProvider.Now;
                    value = entry.Value;
                    return true;
                }

                value = default!;
                expired = false;
                return false;
            }
        }

        /// <summary>
        /// Removes the entry only when its value is <paramref name="expected"/> by reference.
        /// </summary>
        /// <param name="requestId">The id of the entry.</param>
        /// <param name="expected">The value that must own the entry.</param>
        /// <param name="value">The removed value, or the default when the entry was not removed.</param>
        /// <param name="expired">Whether the removed entry had already expired.</param>
        /// <returns><see langword="true"/> when an entry was removed; otherwise
        /// <see langword="false"/>.</returns>
        public bool TryTakeExact(ushort requestId, object expected, out TValue value, out bool expired)
        {
            lock (_locker)
            {
                if (_entries.TryGetValue(requestId, out Entry entry) &&
                    ReferenceEquals(entry.Value, expected))
                {
                    _entries.Remove(requestId);
                    expired = entry.ExpiresAt <= _timeProvider.Now;
                    value = entry.Value;
                    return true;
                }

                value = default!;
                expired = false;
                return false;
            }
        }

        /// <summary>
        /// Removes the entry only when its value is <paramref name="expected"/> and it has already
        /// expired.
        /// </summary>
        /// <returns><see langword="true"/> when an expired entry was removed; otherwise
        /// <see langword="false"/>.</returns>
        public bool TryReapExpired(ushort requestId, object expected)
        {
            lock (_locker)
            {
                if (_entries.TryGetValue(requestId, out Entry entry) &&
                    ReferenceEquals(entry.Value, expected) &&
                    entry.ExpiresAt <= _timeProvider.Now)
                {
                    _entries.Remove(requestId);
                    return true;
                }

                return false;
            }
        }

        /// <summary>
        /// Removes every entry.
        /// </summary>
        /// <param name="onRemoved">Invoked for every removed value while the table is locked.</param>
        public void Clear(Action<TValue>? onRemoved = null)
        {
            lock (_locker)
            {
                if (onRemoved != null)
                {
                    foreach (Entry entry in _entries.Values)
                    {
                        onRemoved(entry.Value);
                    }
                }

                _entries.Clear();
            }
        }

        private void Sweep(DateTime now, Action<TValue>? onExpired)
        {
            List<ushort>? expired = null;

            foreach (KeyValuePair<ushort, Entry> pair in _entries)
            {
                if (pair.Value.ExpiresAt <= now)
                {
                    (expired ??= new List<ushort>()).Add(pair.Key);
                }
            }

            if (expired == null)
            {
                return;
            }

            foreach (ushort requestId in expired)
            {
                if (_entries.TryGetValue(requestId, out Entry entry))
                {
                    _entries.Remove(requestId);
                    onExpired?.Invoke(entry.Value);
                }
            }
        }
    }
}
