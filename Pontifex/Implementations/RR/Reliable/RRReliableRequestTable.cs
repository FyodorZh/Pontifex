using System;
using System.Collections.Generic;

namespace Pontifex.RR.Reliable
{
    /// <summary>
    /// One entry of the reliable request table: the bound handler and its request state.
    /// </summary>
    internal readonly struct RRReliableRequestEntry
    {
        /// <summary>
        /// Initializes an entry.
        /// </summary>
        public RRReliableRequestEntry(IRRReliableRequestHandler handler, RRReliableRequestState state)
        {
            Handler = handler ?? throw new ArgumentNullException(nameof(handler));
            State = state ?? throw new ArgumentNullException(nameof(state));
        }

        /// <summary>Gets the bound request handler.</summary>
        public IRRReliableRequestHandler Handler { get; }

        /// <summary>Gets the request state of the entry.</summary>
        public RRReliableRequestState State { get; }
    }

    /// <summary>
    /// Lock-protected table of the live requests of one reliable RR client, keyed by the
    /// client-local request id.
    /// </summary>
    /// <remarks>
    /// The table has no deadline or clock. An inbound response looks an entry up without removing
    /// it, because a reliable request stays live across many responses and one terminal frame. Only
    /// an exact owner removal, a terminal transition, or a clear removes an entry. The caller
    /// owns the frames and the handlers; the table only holds references.
    /// </remarks>
    internal sealed class RRReliableRequestTable
    {
        private readonly object _locker = new object();
        private readonly Dictionary<ushort, RRReliableRequestEntry> _entries =
            new Dictionary<ushort, RRReliableRequestEntry>();

        /// <summary>Gets the number of live entries.</summary>
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
        /// Adds an entry.
        /// </summary>
        /// <returns><see langword="false"/> when the id is already present.</returns>
        public bool TryAdd(ushort requestId, IRRReliableRequestHandler handler,
            RRReliableRequestState state)
        {
            var entry = new RRReliableRequestEntry(handler, state);

            lock (_locker)
            {
                return _entries.TryAdd(requestId, entry);
            }
        }

        /// <summary>
        /// Looks an entry up without removing it.
        /// </summary>
        public bool TryGet(ushort requestId, out RRReliableRequestEntry entry)
        {
            lock (_locker)
            {
                return _entries.TryGetValue(requestId, out entry);
            }
        }

        /// <summary>
        /// Removes an entry only when its state is <paramref name="expected"/> by reference.
        /// </summary>
        /// <returns><see langword="true"/> when the exact entry was removed.</returns>
        public bool TryTakeExact(ushort requestId, RRReliableRequestState expected,
            out RRReliableRequestEntry entry)
        {
            if (expected == null!)
            {
                throw new ArgumentNullException(nameof(expected));
            }

            lock (_locker)
            {
                if (_entries.TryGetValue(requestId, out RRReliableRequestEntry stored) &&
                    ReferenceEquals(stored.State, expected))
                {
                    _entries.Remove(requestId);
                    entry = stored;
                    return true;
                }

                entry = default;
                return false;
            }
        }

        /// <summary>
        /// Removes the entry with the given id.
        /// </summary>
        public bool TryRemove(ushort requestId, out RRReliableRequestEntry entry)
        {
            lock (_locker)
            {
                return _entries.Remove(requestId, out entry);
            }
        }

        /// <summary>
        /// Removes and returns every entry. The caller processes them outside the table lock, so a
        /// state transition never runs while the table is locked.
        /// </summary>
        public List<RRReliableRequestEntry> TakeAll()
        {
            lock (_locker)
            {
                var entries = new List<RRReliableRequestEntry>(_entries.Values);
                _entries.Clear();
                return entries;
            }
        }

        /// <summary>
        /// Removes every entry.
        /// </summary>
        /// <param name="onRemoved">Invoked for every removed entry while the table is
        /// locked.</param>
        public void Clear(Action<RRReliableRequestEntry>? onRemoved = null)
        {
            lock (_locker)
            {
                if (onRemoved != null)
                {
                    foreach (RRReliableRequestEntry entry in _entries.Values)
                    {
                        onRemoved(entry);
                    }
                }

                _entries.Clear();
            }
        }
    }
}
