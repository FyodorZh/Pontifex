using System;

namespace Pontifex.VirtualDelivery.Netem
{
    public sealed class DistributionTable
    {
        private const long DistScale = 8192;

        private readonly short[] _table;

        public DistributionTable(short[] table)
        {
            if (table == null)
            {
                throw new ArgumentNullException(nameof(table));
            }

            if (table.Length == 0)
            {
                throw new ArgumentException(
                    "A distribution table cannot be empty.", nameof(table));
            }

            _table = table;
        }

        public long Sample(long mu, long sigma, CorrelatedRandom crng)
        {
            if (sigma == 0)
                return mu;

            uint rnd = crng.Next();

            long t = _table[rnd % _table.Length];
            long sigmaMod = sigma % DistScale;
            long x = sigmaMod * t;

            if (x >= 0)
                x += DistScale / 2;
            else
                x -= DistScale / 2;

            return x / DistScale + (sigma / DistScale) * t + mu;
        }
    }
}
