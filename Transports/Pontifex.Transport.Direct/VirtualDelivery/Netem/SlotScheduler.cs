using System;
using System.Diagnostics;

namespace Pontifex.VirtualDelivery.Netem
{
    public sealed class SlotScheduler
    {
        private static readonly int MaxPacketsDefault = int.MaxValue;
        private static readonly int MaxBytesDefault = int.MaxValue;

        private readonly SlotConfig _config;
        private readonly DistributionTable? _dist;

        private long _slotNext;
        private int _packetsLeft;
        private int _bytesLeft;

        public long NextSlotTime => _slotNext;

        public SlotScheduler(SlotConfig config, DistributionTable? dist)
        {
            _config = config;
            _dist = dist;

            int maxPackets = config.MaxPackets > 0 ? config.MaxPackets : MaxPacketsDefault;
            int maxBytes = config.MaxBytes > 0 ? config.MaxBytes : MaxBytesDefault;
            _packetsLeft = maxPackets;
            _bytesLeft = maxBytes;

            if (config.IsEnabled)
                _slotNext = CurrentTimeNs();
        }

        public bool IsOpen(long now)
        {
            return _slotNext == 0 || _slotNext <= now;
        }

        public void Consume(long now, int packetSize)
        {
            if (_slotNext == 0)
                return;

            _packetsLeft--;
            _bytesLeft -= packetSize;

            if (_packetsLeft <= 0 || _bytesLeft <= 0)
            {
                if (_dist == null)
                {
                    long range = _config.MaxDelayNs - _config.MinDelayNs;
                    long delay = _config.MinDelayNs;
                    if (range > 0)
                        delay += (long)((ulong)new Random().Next() * (ulong)range >> 32);
                    _slotNext = now + delay;
                }
                else
                {
                    long nextDelay = _dist.Sample(_config.DistDelayNs, _config.DistJitterNs, new CorrelatedRandom(0));
                    _slotNext = now + nextDelay;
                }

                int maxPackets = _config.MaxPackets > 0 ? _config.MaxPackets : MaxPacketsDefault;
                int maxBytes = _config.MaxBytes > 0 ? _config.MaxBytes : MaxBytesDefault;
                _packetsLeft = maxPackets;
                _bytesLeft = maxBytes;
            }
        }

        private static long CurrentTimeNs()
        {
            return Stopwatch.GetTimestamp() * 1_000_000_000L / Stopwatch.Frequency;
        }

        public void Reset()
        {
            int maxPackets = _config.MaxPackets > 0 ? _config.MaxPackets : MaxPacketsDefault;
            int maxBytes = _config.MaxBytes > 0 ? _config.MaxBytes : MaxBytesDefault;
            _packetsLeft = maxPackets;
            _bytesLeft = maxBytes;

            if (_config.IsEnabled)
                _slotNext = CurrentTimeNs();
            else
                _slotNext = 0;
        }
    }
}
