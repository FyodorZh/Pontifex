using System;

namespace Pontifex.VirtualDelivery.Netem
{
    public enum LossModelKind
    {
        Random,
        FourState,
        GilbertElliot
    }

    public readonly struct FourStateParams
    {
        public readonly uint P13;
        public readonly uint P31;
        public readonly uint P32;
        public readonly uint P14;
        public readonly uint P23;

        public FourStateParams(uint p13, uint p31, uint p32, uint p14, uint p23)
        {
            P13 = p13;
            P31 = p31;
            P32 = p32;
            P14 = p14;
            P23 = p23;
        }
    }

    public readonly struct GilbertElliotParams
    {
        public readonly uint P;
        public readonly uint R;
        public readonly uint H;
        public readonly uint K1;

        public GilbertElliotParams(uint p, uint r, uint h, uint k1)
        {
            P = p;
            R = r;
            H = h;
            K1 = k1;
        }
    }

    public readonly struct CorrelationParams
    {
        public readonly uint DelayRho;
        public readonly uint LossRho;
        public readonly uint DuplicateRho;
        public readonly uint ReorderRho;

        public CorrelationParams(uint delayRho, uint lossRho, uint duplicateRho, uint reorderRho)
        {
            DelayRho = delayRho;
            LossRho = lossRho;
            DuplicateRho = duplicateRho;
            ReorderRho = reorderRho;
        }
    }

    public readonly struct SlotConfig
    {
        public readonly long MinDelayNs;
        public readonly long MaxDelayNs;
        public readonly long DistDelayNs;
        public readonly long DistJitterNs;
        public readonly int MaxPackets;
        public readonly int MaxBytes;

        public SlotConfig(long minDelayNs, long maxDelayNs, long distDelayNs, long distJitterNs, int maxPackets, int maxBytes)
        {
            MinDelayNs = minDelayNs;
            MaxDelayNs = maxDelayNs;
            DistDelayNs = distDelayNs;
            DistJitterNs = distJitterNs;
            MaxPackets = maxPackets;
            MaxBytes = maxBytes;
        }

        public bool IsEnabled => MinDelayNs != 0 || MaxDelayNs != 0 || DistJitterNs != 0;
    }

    public sealed class NetemConfig
    {
        public long LatencyNs { get; init; }
        public long JitterNs { get; init; }
        public uint LossProbability { get; init; }
        public uint DuplicateProbability { get; init; }
        public uint ReorderProbability { get; init; }
        public uint Gap { get; init; }
        public ulong RateBytesPerSec { get; init; }
        public int PacketOverhead { get; init; }
        public uint CellSize { get; init; }
        public int CellOverhead { get; init; }
        public int QueueLimit { get; init; } = int.MaxValue;
        public DistributionTable? DelayDistribution { get; init; }
        public LossModelKind LossModel { get; init; } = LossModelKind.Random;
        public FourStateParams? FourState { get; init; }
        public GilbertElliotParams? GilbertElliot { get; init; }
        public CorrelationParams Correlation { get; init; }
        public SlotConfig Slot { get; init; }
        public DistributionTable? SlotDistribution { get; init; }
    }
}
