using System;

namespace Pontifex.VirtualDelivery.Netem
{
    public sealed class RateLimiter
    {
        private const long NsPerSec = 1_000_000_000L;

        private readonly ulong _rateBytesPerSec;
        private readonly int _packetOverhead;
        private readonly uint _cellSize;
        private readonly int _cellOverhead;

        public RateLimiter(ulong rateBytesPerSec, int packetOverhead, uint cellSize, int cellOverhead)
        {
            _rateBytesPerSec = rateBytesPerSec;
            _packetOverhead = packetOverhead;
            _cellSize = cellSize;
            _cellOverhead = cellOverhead;
        }

        public bool IsEnabled => _rateBytesPerSec > 0;

        public long PacketTimeNs(int packetSize)
        {
            long len = packetSize + _packetOverhead;

            if (_cellSize > 0)
            {
                long cells = len / _cellSize;
                if (len > cells * (long)_cellSize)
                    cells++;
                len = cells * ((long)_cellSize + _cellOverhead);
            }

            return (long)((ulong)len * (ulong)NsPerSec / _rateBytesPerSec);
        }
    }
}
