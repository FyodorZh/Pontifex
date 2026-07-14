using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Actuarius.Memory;
using Pontifex.Utils;

namespace Pontifex.VirtualDelivery.Netem
{
    public sealed class NetemDeliverySystem : IDeliverySystem, IDisposable
    {
        private readonly object _lock = new();
        private readonly Queue<QueueEntry> _reorderQueue = new();
        private readonly List<QueueEntry> _timeQueue = new();
        private readonly AutoResetEvent _signal = new(false);
        private readonly CancellationTokenSource _cts = new();
        private readonly Thread _dequeueThread;
        private readonly NetemConfig _config;
        private readonly ICollectablePool _collectablePool;
        private readonly IPool<IMultiRefByteArray, int> _bytesPool;
        private readonly ILossModel _lossModel;
        private readonly RateLimiter _rateLimiter;
        private readonly SlotScheduler _slotScheduler;
        private readonly CorrelatedRandom _delayCrng;
        private readonly CorrelatedRandom _dupCrng;
        private readonly CorrelatedRandom _reorderCrng;
        private uint _gapCounter;
        private volatile bool _cleared;
        private bool _disposed;

        public event Action<UnionDataList>? Delivered;

        public NetemDeliverySystem(
            NetemConfig config,
            ICollectablePool collectablePool,
            IPool<IMultiRefByteArray, int> bytesPool)
        {
            _config = config;
            _collectablePool = collectablePool;
            _bytesPool = bytesPool;

            _delayCrng = new CorrelatedRandom(config.Correlation.DelayRho);
            _dupCrng = new CorrelatedRandom(config.Correlation.DuplicateRho);
            _reorderCrng = new CorrelatedRandom(config.Correlation.ReorderRho);

            _lossModel = CreateLossModel(config);
            _rateLimiter = new RateLimiter(config.RateBytesPerSec, config.PacketOverhead, config.CellSize, config.CellOverhead);
            _slotScheduler = new SlotScheduler(config.Slot, config.SlotDistribution);

            _dequeueThread = new Thread(DequeueLoop)
            {
                IsBackground = true,
                Name = "NetemDequeue"
            };
            _dequeueThread.Start();
        }

        public void Deliver(UnionDataList message)
        {
            if (_cleared)
            {
                message.Release();
                return;
            }

            if (_disposed)
                throw new ObjectDisposedException(nameof(NetemDeliverySystem));

            lock (_lock)
            {
                int dataSize = message.GetDataSize();
                int count = 1;

                if (_config.DuplicateProbability != 0 && _config.DuplicateProbability >= _dupCrng.Next())
                    count++;

                if (_lossModel.ShouldDrop())
                    count--;

                if (count == 0)
                {
                    message.Release();
                    return;
                }

                int totalQueued = _reorderQueue.Count + _timeQueue.Count;
                if (totalQueued >= _config.QueueLimit)
                {
                    message.Release();
                    return;
                }

                long now = CurrentTimeNs();
                bool shouldReorder = CheckReorder();

                if (shouldReorder)
                {
                    for (int i = 0; i < count; i++)
                    {
                        UnionDataList msg = (i < count - 1) ? CloneMessage(message) : message;
                        _reorderQueue.Enqueue(new QueueEntry(msg, 0, dataSize));
                    }

                    _signal.Set();
                }
                else
                {
                    long delay = CalculateBaseDelay();

                    if (_rateLimiter.IsEnabled)
                    {
                        long lastTimeToSend = GetLastTimeToSend();
                        if (lastTimeToSend > 0)
                        {
                            delay -= lastTimeToSend - now;
                            if (delay < 0)
                                delay = 0;
                            now = lastTimeToSend;
                        }

                        delay += _rateLimiter.PacketTimeNs(dataSize);
                    }

                    long timeToSend = now + delay;

                    for (int i = 0; i < count; i++)
                    {
                        UnionDataList msg = (i < count - 1) ? CloneMessage(message) : message;
                        InsertSorted(new QueueEntry(msg, timeToSend, dataSize));
                    }

                    _signal.Set();
                }
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                _cleared = true;

                while (_reorderQueue.Count > 0)
                {
                    var entry = _reorderQueue.Dequeue();
                    entry.Message.Release();
                }

                foreach (var entry in _timeQueue)
                    entry.Message.Release();

                _timeQueue.Clear();
            }

            _signal.Set();
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _cts.Cancel();
            _signal.Set();

            _dequeueThread.Join(TimeSpan.FromSeconds(5));

            lock (_lock)
            {
                while (_reorderQueue.Count > 0)
                {
                    var entry = _reorderQueue.Dequeue();
                    entry.Message.Release();
                }

                foreach (var entry in _timeQueue)
                {
                    entry.Message.Release();
                }

                _timeQueue.Clear();
            }

            _cts.Dispose();
            _signal.Dispose();
        }

        private static long CurrentTimeNs()
        {
            return Stopwatch.GetTimestamp() * 1_000_000_000L / Stopwatch.Frequency;
        }

        private bool CheckReorder()
        {
            if (_config.Gap == 0)
                return false;

            if (_gapCounter < _config.Gap - 1)
            {
                _gapCounter++;
                return false;
            }

            _gapCounter = 0;

            return _config.ReorderProbability != 0 &&
                   _config.ReorderProbability < _reorderCrng.Next();
        }

        private long CalculateBaseDelay()
        {
            if (_config.DelayDistribution != null)
            {
                return _config.DelayDistribution.Sample(_config.LatencyNs, _config.JitterNs, _delayCrng);
            }

            if (_config.JitterNs == 0)
                return _config.LatencyNs;

            uint rnd = _delayCrng.Next();
            long jitterRange = 2 * _config.JitterNs;
            return ((long)(rnd % (ulong)jitterRange) + _config.LatencyNs) - _config.JitterNs;
        }

        private long GetLastTimeToSend()
        {
            if (_timeQueue.Count > 0)
                return _timeQueue[_timeQueue.Count - 1].TimeToSend;

            return 0;
        }

        private void InsertSorted(QueueEntry entry)
        {
            int index = _timeQueue.BinarySearch(entry, QueueEntryTimeComparer.Instance);
            if (index < 0)
                index = ~index;

            _timeQueue.Insert(index, entry);
        }

        private UnionDataList CloneMessage(UnionDataList source)
        {
            if (!source.Serialize(_bytesPool, out var serializedData))
                throw new InvalidOperationException("Failed to serialize message for cloning");

            try
            {
                var clone = _collectablePool.Acquire<UnionDataList>();
                var byteSource = new ByteSourceFromArray(serializedData);
                if (!clone.Deserialize(ref byteSource, _bytesPool))
                {
                    clone.Release();
                    throw new InvalidOperationException("Failed to deserialize cloned message");
                }

                return clone;
            }
            finally
            {
                serializedData.Release();
            }
        }

        private void DequeueLoop()
        {
            while (!_cts.IsCancellationRequested)
            {
                QueueEntry? entry = null;
                int waitMs = Timeout.Infinite;
                long now = CurrentTimeNs();

                lock (_lock)
                {
                    if (_reorderQueue.Count > 0)
                    {
                        entry = _reorderQueue.Dequeue();
                    }
                    else if (_timeQueue.Count > 0)
                    {
                        var peek = _timeQueue[0];
                        if (peek.TimeToSend <= now && _slotScheduler.IsOpen(now))
                        {
                            entry = peek;
                            _timeQueue.RemoveAt(0);
                            _slotScheduler.Consume(now, entry.DataSize);
                        }
                        else
                        {
                            long nextWake = peek.TimeToSend;
                            long slotNext = _slotScheduler.NextSlotTime;
                            if (slotNext != 0 && slotNext > now)
                                nextWake = Math.Max(nextWake, slotNext);

                            waitMs = (int)Math.Min((nextWake - now) / 1_000_000L, int.MaxValue);
                            if (waitMs < 0)
                                waitMs = 0;
                        }
                    }
                }

                if (entry != null)
                {
                    if (_cleared)
                    {
                        entry.Message.Release();
                    }
                    else
                    {
                        var delivered = Delivered;
                        if (delivered != null)
                            delivered(entry.Message);
                        else
                            entry.Message.Release();
                    }
                }
                else
                {
                    _signal.WaitOne(waitMs);
                }
            }
        }

        private static ILossModel CreateLossModel(NetemConfig config)
        {
            switch (config.LossModel)
            {
                case LossModelKind.Random:
                    return new RandomLossModel(config.LossProbability,
                        new CorrelatedRandom(config.Correlation.LossRho));

                case LossModelKind.FourState:
                    if (config.FourState == null)
                        throw new ArgumentException("FourState loss model requires FourStateParams");
                    return new FourStateLossModel(config.FourState.Value);

                case LossModelKind.GilbertElliot:
                    if (config.GilbertElliot == null)
                        throw new ArgumentException("GilbertElliot loss model requires GilbertElliotParams");
                    return new GilbertElliotLossModel(config.GilbertElliot.Value);

                default:
                    throw new ArgumentOutOfRangeException(nameof(config.LossModel));
            }
        }

        private sealed class QueueEntry
        {
            public readonly UnionDataList Message;
            public readonly long TimeToSend;
            public readonly int DataSize;

            public QueueEntry(UnionDataList message, long timeToSend, int dataSize)
            {
                Message = message;
                TimeToSend = timeToSend;
                DataSize = dataSize;
            }
        }

        private sealed class QueueEntryTimeComparer : IComparer<QueueEntry>
        {
            public static readonly QueueEntryTimeComparer Instance = new();

            public int Compare(QueueEntry? x, QueueEntry? y)
            {
                if (x == null && y == null) return 0;
                if (x == null) return -1;
                if (y == null) return 1;
                return x.TimeToSend.CompareTo(y.TimeToSend);
            }
        }
    }
}
