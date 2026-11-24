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

        /// <summary>
        /// Validates the configuration values that the delivery system cannot interpret.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">A negative duration, overhead, or queue limit was
        /// supplied.</exception>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Other notes</b>: Cells are enabled exactly when <see cref="CellSize"/> is
        /// greater than zero; zero disables them. The loss-model parameter set is validated by the delivery
        /// system factory. A negative <see cref="CellSize"/> is impossible because the type is
        /// <see cref="uint"/>.</description></item>
        /// </list>
        /// </remarks>
        public void Validate()
        {
            RequireNonNegative(LatencyNs, nameof(LatencyNs));
            RequireNonNegative(JitterNs, nameof(JitterNs));
            RequireNonNegative(PacketOverhead, nameof(PacketOverhead));
            RequireNonNegative(CellOverhead, nameof(CellOverhead));
            RequireNonNegative(QueueLimit, nameof(QueueLimit));
            RequireNonNegative(Slot.MinDelayNs, nameof(Slot) + "." + nameof(Slot.MinDelayNs));
            RequireNonNegative(Slot.MaxDelayNs, nameof(Slot) + "." + nameof(Slot.MaxDelayNs));
            RequireNonNegative(Slot.DistDelayNs, nameof(Slot) + "." + nameof(Slot.DistDelayNs));
            RequireNonNegative(Slot.DistJitterNs, nameof(Slot) + "." + nameof(Slot.DistJitterNs));
            RequireNonNegative(Slot.MaxPackets, nameof(Slot) + "." + nameof(Slot.MaxPackets));
            RequireNonNegative(Slot.MaxBytes, nameof(Slot) + "." + nameof(Slot.MaxBytes));
        }

        private static void RequireNonNegative(long value, string name)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(name,
                    "The netem configuration value must not be negative.");
            }
        }
    }
}
