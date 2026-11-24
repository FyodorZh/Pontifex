using System;
using Actuarius.Memory;
using Pontifex.Factory.Registration;
using Pontifex.VirtualDelivery;
using Pontifex.VirtualDelivery.Netem;

namespace Pontifex.Raw.Unreliable.Direct
{
    /// <summary>
    /// Shared factory-binding support for the unreliable Direct transports. The
    /// formula-facing delivery value is a typed struct that is mapped directly to
    /// the virtual-delivery configuration.
    /// </summary>
    internal static class UnreliableDeliveryBinding
    {
        private static readonly StructSchema DeliverySchema = CreateDeliverySchema();

        public static StructParameter CreateParameter()
        {
            return new StructParameter(
                "delivery",
                required: false,
                positional: false,
                DeliverySchema);
        }

        public static IDeliverySystem Build(
            ITransportStructValue value,
            IMemoryRental memory)
        {
            return CreateFactory(value, memory)();
        }

        /// <summary>
        /// Validates a delivery value and returns a factory that constructs a fresh delivery system on each
        /// call.
        /// </summary>
        /// <param name="value">The delivery value to validate and parse.</param>
        /// <param name="memory">The memory rental used by a netem system.</param>
        /// <returns>A factory that constructs a new, independent delivery system per invocation.</returns>
        /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">The value is structurally invalid, names an unknown type, or
        /// selects an incomplete loss model.</exception>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Other notes</b>: Validation and parsing run eagerly here; the returned
        /// factory captures only the immutable parsed configuration and the memory rental, never the mutable
        /// <see cref="ITransportStructValue"/>.</description></item>
        /// </list>
        /// </remarks>
        public static Func<IDeliverySystem> CreateFactory(
            ITransportStructValue value,
            IMemoryRental memory)
        {
            if (value is null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            if (memory is null)
            {
                throw new ArgumentNullException(nameof(memory));
            }

            if (!value.TryGetString("type", out var type))
            {
                throw new ArgumentException(
                    "The 'delivery' value requires a 'type' field.", nameof(value));
            }

            switch (type)
            {
                case "perfect":
                    return () => new PerfectDeliverySystem();
                case "netem":
                    var config = ParseNetemConfig(value);
                    config.Validate();
                    _ = NetemDeliverySystem.CreateLossModel(config);
                    return () => new NetemDeliverySystem(
                        config,
                        memory.CollectablePool,
                        memory.ByteArraysPool);
                default:
                    throw new ArgumentException(
                        $"Unknown delivery type '{type}'.", nameof(value));
            }
        }

        /// <summary>
        /// Validates a delivery value without constructing a threaded delivery system.
        /// </summary>
        /// <param name="value">The delivery value to validate.</param>
        /// <exception cref="ArgumentException">The value is structurally invalid, names an unknown
        /// type, or selects a loss model without its parameters.</exception>
        internal static void Validate(ITransportStructValue value)
        {
            if (value is null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            if (!value.TryGetString("type", out var type))
            {
                throw new ArgumentException(
                    "The 'delivery' value requires a 'type' field.", nameof(value));
            }

            if (TryGetString(value, "lossmodel", out var configuredLossModel))
            {
                _ = ParseLossModelKind(configuredLossModel);
            }

            switch (type)
            {
                case "perfect":
                    return;
                case "netem":
                    var config = ParseNetemConfig(value);
                    config.Validate();
                    _ = NetemDeliverySystem.CreateLossModel(config);
                    return;
                default:
                    throw new ArgumentException(
                        $"Unknown delivery type '{type}'.", nameof(value));
            }
        }

        private static IDeliverySystem BuildNetem(
            ITransportStructValue value,
            IMemoryRental memory)
        {
            var config = ParseNetemConfig(value);

            try
            {
                return new NetemDeliverySystem(
                    config,
                    memory.CollectablePool,
                    memory.ByteArraysPool);
            }
            catch (Exception ex)
            {
                throw new ArgumentException(
                    "The netem delivery configuration is invalid.", nameof(value), ex);
            }
        }

        internal static NetemConfig ParseNetemConfig(ITransportStructValue value)
        {
            TryGetLong(value, "latencyns", out var latencyNs);
            TryGetLong(value, "jitterns", out var jitterNs);
            TryGetUInt32(value, "lossprobability", out var lossProbability);
            TryGetUInt32(value, "duplicateprobability", out var duplicateProbability);
            TryGetUInt32(value, "reorderprobability", out var reorderProbability);
            TryGetUInt32(value, "gap", out var gap);
            TryGetUInt64(value, "ratebytespersec", out var rateBytesPerSec);
            TryGetInt32(value, "packetoverhead", out var packetOverhead);
            TryGetUInt32(value, "cellsize", out var cellSize);
            TryGetInt32(value, "celloverhead", out var cellOverhead);
            if (!TryGetInt32(value, "queuelimit", out var queueLimit))
            {
                queueLimit = int.MaxValue;
            }

            var lossModel = LossModelKind.Random;
            if (TryGetString(value, "lossmodel", out var lossModelText))
            {
                lossModel = ParseLossModelKind(lossModelText);
            }

            var correlation = default(CorrelationParams);
            if (TryGetStruct(value, "correlation", out var correlationValue))
            {
                correlation = ParseCorrelation(correlationValue);
            }

            var slot = default(SlotConfig);
            if (TryGetStruct(value, "slot", out var slotValue))
            {
                slot = ParseSlotConfig(slotValue);
            }

            DistributionTable? delayDistribution = null;
            if (TryGetStruct(value, "delaydistribution", out var delayDistributionValue))
            {
                delayDistribution = ParseDistributionTable(delayDistributionValue);
            }

            DistributionTable? slotDistribution = null;
            if (TryGetStruct(value, "slotdistribution", out var slotDistributionValue))
            {
                slotDistribution = ParseDistributionTable(slotDistributionValue);
            }

            FourStateParams? fourState = null;
            if (TryGetStruct(value, "fourstate", out var fourStateValue))
            {
                fourState = ParseFourStateParams(fourStateValue);
            }

            GilbertElliotParams? gilbertElliot = null;
            if (TryGetStruct(value, "gilbertelliot", out var gilbertElliotValue))
            {
                gilbertElliot = ParseGilbertElliotParams(gilbertElliotValue);
            }

            return new NetemConfig
            {
                LatencyNs = latencyNs,
                JitterNs = jitterNs,
                LossProbability = lossProbability,
                DuplicateProbability = duplicateProbability,
                ReorderProbability = reorderProbability,
                Gap = gap,
                RateBytesPerSec = rateBytesPerSec,
                PacketOverhead = packetOverhead,
                CellSize = cellSize,
                CellOverhead = cellOverhead,
                QueueLimit = queueLimit,
                LossModel = lossModel,
                Correlation = correlation,
                Slot = slot,
                DelayDistribution = delayDistribution,
                SlotDistribution = slotDistribution,
                FourState = fourState,
                GilbertElliot = gilbertElliot
            };
        }

        private static LossModelKind ParseLossModelKind(string value)
        {
            switch (value)
            {
                case "random":
                    return LossModelKind.Random;
                case "fourState":
                    return LossModelKind.FourState;
                case "gilbertElliot":
                    return LossModelKind.GilbertElliot;
                default:
                    throw new ArgumentException(
                        $"Unknown loss model '{value}'.", nameof(value));
            }
        }

        private static CorrelationParams ParseCorrelation(ITransportStructValue value)
        {
            TryGetUInt32(value, "delayrho", out var delayRho);
            TryGetUInt32(value, "lossrho", out var lossRho);
            TryGetUInt32(value, "duplicaterho", out var duplicateRho);
            TryGetUInt32(value, "reorderrho", out var reorderRho);

            return new CorrelationParams(delayRho, lossRho, duplicateRho, reorderRho);
        }

        private static SlotConfig ParseSlotConfig(ITransportStructValue value)
        {
            TryGetLong(value, "mindelayns", out var minDelayNs);
            TryGetLong(value, "maxdelayns", out var maxDelayNs);
            TryGetLong(value, "distdelayns", out var distDelayNs);
            TryGetLong(value, "distjitterns", out var distJitterNs);
            TryGetInt32(value, "maxpackets", out var maxPackets);
            TryGetInt32(value, "maxbytes", out var maxBytes);

            return new SlotConfig(
                minDelayNs,
                maxDelayNs,
                distDelayNs,
                distJitterNs,
                maxPackets,
                maxBytes);
        }

        private static DistributionTable ParseDistributionTable(
            ITransportStructValue value)
        {
            if (!value.TryGetArray("values", out var values) || values.Count == 0)
            {
                throw new ArgumentException(
                    "A distribution table requires at least one value.", nameof(value));
            }

            var shorts = new short[values.Count];
            for (var i = 0; i < shorts.Length; i++)
            {
                if (!values.TryGetInt64(i, out var number))
                {
                    throw new ArgumentException(
                        $"The distribution value at index {i} is not an integer.", nameof(value));
                }

                shorts[i] = checked((short)number);
            }

            return new DistributionTable(shorts);
        }

        private static FourStateParams ParseFourStateParams(
            ITransportStructValue value)
        {
            TryGetUInt32(value, "p13", out var p13);
            TryGetUInt32(value, "p31", out var p31);
            TryGetUInt32(value, "p32", out var p32);
            TryGetUInt32(value, "p14", out var p14);
            TryGetUInt32(value, "p23", out var p23);

            return new FourStateParams(p13, p31, p32, p14, p23);
        }

        private static GilbertElliotParams ParseGilbertElliotParams(
            ITransportStructValue value)
        {
            TryGetUInt32(value, "p", out var p);
            TryGetUInt32(value, "r", out var r);
            TryGetUInt32(value, "h", out var h);
            TryGetUInt32(value, "k1", out var k1);

            return new GilbertElliotParams(p, r, h, k1);
        }

        private static bool TryGetLong(
            ITransportStructValue value,
            string fieldName,
            out long number)
        {
            return value.TryGetInt64(fieldName, out number);
        }

        private static bool TryGetInt32(
            ITransportStructValue value,
            string fieldName,
            out int number)
        {
            if (value.TryGetInt64(fieldName, out var value64))
            {
                number = (int)value64;
                return true;
            }

            number = 0;
            return false;
        }

        private static bool TryGetUInt32(
            ITransportStructValue value,
            string fieldName,
            out uint number)
        {
            if (value.TryGetInt64(fieldName, out var value64))
            {
                number = (uint)value64;
                return true;
            }

            number = 0;
            return false;
        }

        private static bool TryGetUInt64(
            ITransportStructValue value,
            string fieldName,
            out ulong number)
        {
            if (value.TryGetInt64(fieldName, out var value64))
            {
                number = (ulong)value64;
                return true;
            }

            number = 0;
            return false;
        }

        private static bool TryGetString(
            ITransportStructValue value,
            string fieldName,
            out string text)
        {
            return value.TryGetString(fieldName, out text);
        }

        private static bool TryGetStruct(
            ITransportStructValue value,
            string fieldName,
            out ITransportStructValue nested)
        {
            return value.TryGetStruct(fieldName, out nested);
        }

        private static StructSchema CreateDeliverySchema()
        {
            var distribution = new StructSchema(new StructField[]
            {
                new ArrayField(
                    "values",
                    required: true,
                    new IntegerType(short.MinValue, short.MaxValue),
                    min: 0)
            });

            var correlation = new StructSchema(new StructField[]
            {
                new IntegerField("delayrho", false, new IntegerType(0, uint.MaxValue)),
                new IntegerField("lossrho", false, new IntegerType(0, uint.MaxValue)),
                new IntegerField("duplicaterho", false, new IntegerType(0, uint.MaxValue)),
                new IntegerField("reorderrho", false, new IntegerType(0, uint.MaxValue))
            });

            var slot = new StructSchema(new StructField[]
            {
                new IntegerField("mindelayns", false, new IntegerType(long.MinValue, long.MaxValue)),
                new IntegerField("maxdelayns", false, new IntegerType(long.MinValue, long.MaxValue)),
                new IntegerField("distdelayns", false, new IntegerType(long.MinValue, long.MaxValue)),
                new IntegerField("distjitterns", false, new IntegerType(long.MinValue, long.MaxValue)),
                new IntegerField("maxpackets", false, new IntegerType(0, int.MaxValue)),
                new IntegerField("maxbytes", false, new IntegerType(0, int.MaxValue))
            });

            var fourState = new StructSchema(new StructField[]
            {
                new IntegerField("p13", false, new IntegerType(0, uint.MaxValue)),
                new IntegerField("p31", false, new IntegerType(0, uint.MaxValue)),
                new IntegerField("p32", false, new IntegerType(0, uint.MaxValue)),
                new IntegerField("p14", false, new IntegerType(0, uint.MaxValue)),
                new IntegerField("p23", false, new IntegerType(0, uint.MaxValue))
            });

            var gilbertElliot = new StructSchema(new StructField[]
            {
                new IntegerField("p", false, new IntegerType(0, uint.MaxValue)),
                new IntegerField("r", false, new IntegerType(0, uint.MaxValue)),
                new IntegerField("h", false, new IntegerType(0, uint.MaxValue)),
                new IntegerField("k1", false, new IntegerType(0, uint.MaxValue))
            });

            return new StructSchema(new StructField[]
            {
                new StringField("type", required: true),
                new IntegerField("latencyns", false, new IntegerType(0, long.MaxValue)),
                new IntegerField("jitterns", false, new IntegerType(0, long.MaxValue)),
                new IntegerField("lossprobability", false, new IntegerType(0, uint.MaxValue)),
                new IntegerField("duplicateprobability", false, new IntegerType(0, uint.MaxValue)),
                new IntegerField("reorderprobability", false, new IntegerType(0, uint.MaxValue)),
                new IntegerField("gap", false, new IntegerType(0, uint.MaxValue)),
                new IntegerField("ratebytespersec", false, new IntegerType(0, long.MaxValue)),
                new IntegerField("packetoverhead", false, new IntegerType(0, int.MaxValue)),
                new IntegerField("cellsize", false, new IntegerType(0, uint.MaxValue)),
                new IntegerField("celloverhead", false, new IntegerType(0, int.MaxValue)),
                new IntegerField("queuelimit", false, new IntegerType(0, int.MaxValue)),
                new StringField("lossmodel", required: false),
                new NestedStructField("correlation", false, correlation),
                new NestedStructField("slot", false, slot),
                new NestedStructField("delaydistribution", false, distribution),
                new NestedStructField("slotdistribution", false, distribution),
                new NestedStructField("fourstate", false, fourState),
                new NestedStructField("gilbertelliot", false, gilbertElliot)
            });
        }
    }
}
