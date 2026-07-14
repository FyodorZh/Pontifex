using System;
using System.Threading;

namespace Pontifex.VirtualDelivery.Netem
{
    public interface ILossModel
    {
        bool ShouldDrop();
        void Reset();
    }

    public sealed class RandomLossModel : ILossModel
    {
        private readonly uint _probability;
        private readonly CorrelatedRandom _crng;

        public RandomLossModel(uint probability, CorrelatedRandom crng)
        {
            _probability = probability;
            _crng = crng;
        }

        public bool ShouldDrop()
        {
            return _probability != 0 && _probability >= _crng.Next();
        }

        public void Reset()
        {
        }
    }

    internal enum FourState
    {
        TxGap = 1,
        TxBurst = 2,
        LostGap = 3,
        LostBurst = 4
    }

    public sealed class FourStateLossModel : ILossModel
    {
        private static readonly ThreadLocal<Random> Rng = new(() => new Random());
        private static readonly ThreadLocal<byte[]> RngBuffer = new(() => new byte[4]);

        private readonly uint _p13;
        private readonly uint _p31;
        private readonly uint _p32;
        private readonly uint _p14;
        private readonly uint _p23;

        private FourState _state;

        public FourStateLossModel(FourStateParams p)
        {
            _p13 = p.P13;
            _p31 = p.P31;
            _p32 = p.P32;
            _p14 = p.P14;
            _p23 = p.P23;
            _state = FourState.TxGap;
        }

        public bool ShouldDrop()
        {
            uint rnd = NextU32();

            switch (_state)
            {
                case FourState.TxGap:
                    if (rnd < _p14)
                    {
                        _state = FourState.LostBurst;
                        return true;
                    }
                    else if (_p14 < rnd && rnd < _p14 + _p13)
                    {
                        _state = FourState.LostGap;
                        return true;
                    }
                    else
                    {
                        _state = FourState.TxGap;
                    }
                    break;

                case FourState.TxBurst:
                    if (rnd < _p23)
                    {
                        _state = FourState.LostGap;
                        return true;
                    }
                    else
                    {
                        _state = FourState.TxBurst;
                    }
                    break;

                case FourState.LostGap:
                    if (rnd < _p32)
                    {
                        _state = FourState.TxBurst;
                    }
                    else if (_p32 < rnd && rnd < _p32 + _p31)
                    {
                        _state = FourState.TxGap;
                    }
                    else
                    {
                        _state = FourState.LostGap;
                        return true;
                    }
                    break;

                case FourState.LostBurst:
                    _state = FourState.TxGap;
                    break;
            }

            return false;
        }

        public void Reset()
        {
            _state = FourState.TxGap;
        }

        private static uint NextU32()
        {
            var buf = RngBuffer.Value!;
            Rng.Value!.NextBytes(buf);
            return BitConverter.ToUInt32(buf, 0);
        }
    }

    internal enum GeState
    {
        Good = 1,
        Bad = 2
    }

    public sealed class GilbertElliotLossModel : ILossModel
    {
        private static readonly ThreadLocal<Random> Rng = new(() => new Random());
        private static readonly ThreadLocal<byte[]> RngBuffer = new(() => new byte[4]);

        private readonly uint _p;
        private readonly uint _r;
        private readonly uint _h;
        private readonly uint _k1;

        private GeState _state;

        public GilbertElliotLossModel(GilbertElliotParams p)
        {
            _p = p.P;
            _r = p.R;
            _h = p.H;
            _k1 = p.K1;
            _state = GeState.Good;
        }

        public bool ShouldDrop()
        {
            switch (_state)
            {
                case GeState.Good:
                    if (NextU32() < _p)
                        _state = GeState.Bad;
                    if (NextU32() < _k1)
                        return true;
                    break;

                case GeState.Bad:
                    if (NextU32() < _r)
                        _state = GeState.Good;
                    if (NextU32() > _h)
                        return true;
                    break;
            }

            return false;
        }

        public void Reset()
        {
            _state = GeState.Good;
        }

        private static uint NextU32()
        {
            var buf = RngBuffer.Value!;
            Rng.Value!.NextBytes(buf);
            return BitConverter.ToUInt32(buf, 0);
        }
    }
}
