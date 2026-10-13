using Actuarius.Memory;

namespace Pontifex.Tests.NetSockets.LowLevelTcpSocketTests.Harness
{
    /// <summary>
    /// Application-level byte-budget ledger and weighted admission example. This type is the
    /// executable composition example referenced by the transport documentation: it lives entirely
    /// in the application/test layer, is never used by the transport hot path, and is not transport
    /// flow control. It tracks lifecycle phases (queued / native-pending / consumer-held /
    /// deferred-cleanup) and two distinct metrics:
    /// <list type="bullet">
    /// <item><description><see cref="LogicalBytes"/> — application bookkeeping over the requested
    /// byte counts of tracked items (sum of the phase metrics);</description></item>
    /// <item><description><see cref="PhysicalRetainedBytes"/> — deduplicated physical raw-array
    /// capacity, counting each distinct backing array once (a partial view and its owner share the
    /// same array).</description></item>
    /// </list>
    /// Admission is byte-weighted, not item-counted; a rejection never touches the item or the
    /// budget. The consumer credit is released exactly once by the application's final release
    /// (<see cref="ConsumerRelease"/>); logical task publication is recorded but is not a release
    /// trigger.
    /// </summary>
    internal sealed class ApplicationByteLedger
    {
        private enum ItemPhase
        {
            Queued,
            Pending,
            ConsumerHeld,
            DeferredCleanup,
        }

        private sealed class Entry
        {
            public ItemPhase Phase;
            public Task? LogicalCompletion;
        }

        private readonly long _budgetBytes;
        private readonly object _sync = new();
        private readonly Dictionary<IMultiRefByteArray, Entry> _items = new(ReferenceEqualityComparer.Instance);
        private readonly Dictionary<byte[], int> _physicalRefs = new(ReferenceEqualityComparer.Instance);

        private long _queuedBytes;
        private long _pendingBytes;
        private long _consumerHeldBytes;
        private long _deferredCleanupBytes;
        private long _physicalRetainedBytes;
        private int _misuseCount;

        public ApplicationByteLedger(long budgetBytes)
        {
            if (budgetBytes <= 0) throw new ArgumentOutOfRangeException(nameof(budgetBytes));
            _budgetBytes = budgetBytes;
        }

        public long BudgetBytes => _budgetBytes;

        public long QueuedBytes { get { lock (_sync) { return _queuedBytes; } } }

        public long PendingBytes { get { lock (_sync) { return _pendingBytes; } } }

        public long ConsumerHeldBytes { get { lock (_sync) { return _consumerHeldBytes; } } }

        public long DeferredCleanupBytes { get { lock (_sync) { return _deferredCleanupBytes; } } }

        public long LogicalBytes
            => QueuedBytes + PendingBytes + ConsumerHeldBytes + DeferredCleanupBytes;

        public long PhysicalRetainedBytes { get { lock (_sync) { return _physicalRetainedBytes; } } }

        public int PhysicalRootCount { get { lock (_sync) { return _physicalRefs.Count; } } }

        public int MisuseCount { get { lock (_sync) { return _misuseCount; } } }

        /// <summary>
        /// Byte-weighted admission. The item's requested byte count is charged against the budget
        /// across every tracked phase; a rejection leaves the item and all counters untouched (the
        /// producer keeps ownership of its reference).
        /// </summary>
        public bool TryAdmitQueued(IMultiRefByteArray item)
        {
            lock (_sync)
            {
                long bytes = item.Count;
                long logical = _queuedBytes + _pendingBytes + _consumerHeldBytes + _deferredCleanupBytes;
                if (logical + bytes > _budgetBytes)
                {
                    return false;
                }

                _items[item] = new Entry { Phase = ItemPhase.Queued };
                _queuedBytes += bytes;
                AddPhysicalLocked(item);
                return true;
            }
        }

        public void MarkPending(IMultiRefByteArray item) => Transition(item, ItemPhase.Pending);

        /// <summary>
        /// Moves a delivered lease to the consumer-held phase. The optional completion task is
        /// recorded for diagnostics only: publication must not release the held credit.
        /// </summary>
        public void MarkConsumerHeld(IMultiRefByteArray item, Task? logicalCompletion = null)
        {
            lock (_sync)
            {
                if (!_items.TryGetValue(item, out Entry? entry))
                {
                    _misuseCount++;
                    return;
                }

                RemovePhaseBytesLocked(item, entry.Phase);
                entry.Phase = ItemPhase.ConsumerHeld;
                entry.LogicalCompletion = logicalCompletion;
                _consumerHeldBytes += item.Count;
            }
        }

        public void MarkDeferredCleanup(IMultiRefByteArray item) => Transition(item, ItemPhase.DeferredCleanup);

        /// <summary>Removes an item from any phase exactly once (producer drain / deferred cleanup done).</summary>
        public void Remove(IMultiRefByteArray item)
        {
            lock (_sync)
            {
                if (!_items.TryGetValue(item, out Entry? entry))
                {
                    _misuseCount++;
                    return;
                }

                RemovePhaseBytesLocked(item, entry.Phase);
                _items.Remove(item);
                RemovePhysicalLocked(item);
            }
        }

        /// <summary>
        /// The application's final release of a consumer-held lease: releases the credit exactly
        /// once. Task publication is not a release trigger; a second release is application misuse
        /// and does not change the counters.
        /// </summary>
        public void ConsumerRelease(IMultiRefByteArray item)
        {
            lock (_sync)
            {
                if (!_items.TryGetValue(item, out Entry? entry) || entry.Phase != ItemPhase.ConsumerHeld)
                {
                    _misuseCount++;
                    return;
                }

                _items.Remove(item);
                _consumerHeldBytes -= item.Count;
                RemovePhysicalLocked(item);
            }
        }

        private void Transition(IMultiRefByteArray item, ItemPhase phase)
        {
            lock (_sync)
            {
                if (!_items.TryGetValue(item, out Entry? entry))
                {
                    _misuseCount++;
                    return;
                }

                RemovePhaseBytesLocked(item, entry.Phase);
                entry.Phase = phase;
                switch (phase)
                {
                    case ItemPhase.Pending:
                        _pendingBytes += item.Count;
                        break;
                    case ItemPhase.DeferredCleanup:
                        _deferredCleanupBytes += item.Count;
                        break;
                    default:
                        _misuseCount++;
                        break;
                }
            }
        }

        private void AddPhysicalLocked(IMultiRefByteArray item)
        {
            byte[]? array = item.Array;
            if (array == null)
            {
                return;
            }

            if (_physicalRefs.TryGetValue(array, out int references))
            {
                _physicalRefs[array] = references + 1;
            }
            else
            {
                _physicalRefs[array] = 1;
                _physicalRetainedBytes += array.Length;
            }
        }

        private void RemovePhysicalLocked(IMultiRefByteArray item)
        {
            byte[]? array = item.Array;
            if (array == null || !_physicalRefs.TryGetValue(array, out int references))
            {
                return;
            }

            if (references <= 1)
            {
                _physicalRefs.Remove(array);
                _physicalRetainedBytes -= array.Length;
            }
            else
            {
                _physicalRefs[array] = references - 1;
            }
        }

        private void RemovePhaseBytesLocked(IMultiRefByteArray item, ItemPhase phase)
        {
            switch (phase)
            {
                case ItemPhase.Queued:
                    _queuedBytes -= item.Count;
                    break;
                case ItemPhase.Pending:
                    _pendingBytes -= item.Count;
                    break;
                case ItemPhase.ConsumerHeld:
                    _consumerHeldBytes -= item.Count;
                    break;
                case ItemPhase.DeferredCleanup:
                    _deferredCleanupBytes -= item.Count;
                    break;
            }
        }
    }
}
