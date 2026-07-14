using System;

namespace Pontifex.VirtualDelivery.Netem
{
    public sealed class CorrelatedRandom
    {
        private uint _last;
        private uint _rho;
        private readonly Random _rng;
        private readonly byte[] _buffer;

        public CorrelatedRandom(uint rho)
        {
            _rng = new Random();
            _buffer = new byte[4];
            _rho = rho;
            _last = NextRaw();
        }

        public CorrelatedRandom(uint rho, int seed)
        {
            _rng = new Random(seed);
            _buffer = new byte[4];
            _rho = rho;
            _last = NextRaw();
        }

        public void Reset(uint rho)
        {
            _rho = rho;
            _last = NextRaw();
        }

        public uint Next()
        {
            if (_rho == 0)
                return NextRaw();

            ulong value = NextRaw();
            ulong rho = (ulong)_rho + 1;
            ulong answer = (value * ((1ul << 32) - rho) + (ulong)_last * rho) >> 32;
            _last = (uint)answer;
            return (uint)answer;
        }

        private uint NextRaw()
        {
            _rng.NextBytes(_buffer);
            return BitConverter.ToUInt32(_buffer, 0);
        }
    }
}
