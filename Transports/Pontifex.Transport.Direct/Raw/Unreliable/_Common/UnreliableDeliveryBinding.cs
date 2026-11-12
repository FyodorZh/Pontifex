using System;
using Actuarius.Memory;
using Pontifex.Factory.Registration;
using Pontifex.OldFactory;
using Pontifex.VirtualDelivery;

namespace Pontifex.Raw.Unreliable.Direct
{
    /// <summary>
    /// Shared factory-binding support for the unreliable Direct transports. The
    /// formula-facing delivery value is a typed struct; this helper converts it
    /// to the legacy description consumed by <see cref="DeliverySystemFactory"/>.
    /// The bridge is transitional until the delivery configuration is expressed
    /// without <see cref="IDescription"/>.
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
            if (value is null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            if (memory is null)
            {
                throw new ArgumentNullException(nameof(memory));
            }

            var description = ToDescription(value);
            return DeliverySystemFactory.Build(
                       description,
                       memory.CollectablePool,
                       memory.ByteArraysPool)
                   ?? new PerfectDeliverySystem();
        }

        private static Description ToDescription(ITransportStructValue value)
        {
            var description = new Description();

            AddString(description, value, "type", "type");
            AddLong(description, value, "latencyns", "latencyNs");
            AddLong(description, value, "jitterns", "jitterNs");
            AddLong(description, value, "lossprobability", "lossProbability");
            AddLong(description, value, "duplicateprobability", "duplicateProbability");
            AddLong(description, value, "reorderprobability", "reorderProbability");
            AddLong(description, value, "gap", "gap");
            AddLong(description, value, "ratebytespersec", "rateBytesPerSec");
            AddLong(description, value, "packetoverhead", "packetOverhead");
            AddLong(description, value, "cellsize", "cellSize");
            AddLong(description, value, "celloverhead", "cellOverhead");
            AddLong(description, value, "queuelimit", "queueLimit");
            AddString(description, value, "lossmodel", "lossModel");

            AddNested(description, value, "correlation", "correlation", ToCorrelation);
            AddNested(description, value, "slot", "slot", ToSlot);
            AddNested(description, value, "delaydistribution", "delayDistribution", ToDistribution);
            AddNested(description, value, "slotdistribution", "slotDistribution", ToDistribution);
            AddNested(description, value, "fourstate", "fourState", ToFourState);
            AddNested(description, value, "gilbertelliot", "gilbertElliot", ToGilbertElliot);

            return description;
        }

        private static Description ToCorrelation(ITransportStructValue value)
        {
            var description = new Description();
            AddLong(description, value, "delayrho", "delayRho");
            AddLong(description, value, "lossrho", "lossRho");
            AddLong(description, value, "duplicaterho", "duplicateRho");
            AddLong(description, value, "reorderrho", "reorderRho");
            return description;
        }

        private static Description ToSlot(ITransportStructValue value)
        {
            var description = new Description();
            AddLong(description, value, "mindelayns", "minDelayNs");
            AddLong(description, value, "maxdelayns", "maxDelayNs");
            AddLong(description, value, "distdelayns", "distDelayNs");
            AddLong(description, value, "distjitterns", "distJitterNs");
            AddLong(description, value, "maxpackets", "maxPackets");
            AddLong(description, value, "maxbytes", "maxBytes");
            return description;
        }

        private static Description ToDistribution(ITransportStructValue value)
        {
            var description = new Description();
            if (!value.TryGetArray("values", out var values))
            {
                return description;
            }

            var elements = new IElement[values.Count];
            for (var i = 0; i < elements.Length; i++)
            {
                elements[i] = values.TryGetInt64(i, out var element)
                    ? new LongElement(element)
                    : VoidElement.Instance;
            }

            description.Add("values", new ArrayElement(elements));
            return description;
        }

        private static Description ToFourState(ITransportStructValue value)
        {
            var description = new Description();
            AddLong(description, value, "p13", "p13");
            AddLong(description, value, "p31", "p31");
            AddLong(description, value, "p32", "p32");
            AddLong(description, value, "p14", "p14");
            AddLong(description, value, "p23", "p23");
            return description;
        }

        private static Description ToGilbertElliot(ITransportStructValue value)
        {
            var description = new Description();
            AddLong(description, value, "p", "p");
            AddLong(description, value, "r", "r");
            AddLong(description, value, "h", "h");
            AddLong(description, value, "k1", "k1");
            return description;
        }

        private static void AddString(
            Description description,
            ITransportStructValue value,
            string fieldName,
            string descriptionName)
        {
            if (value.TryGetString(fieldName, out var text))
            {
                description.Add(descriptionName, new StringElement(text));
            }
        }

        private static void AddLong(
            Description description,
            ITransportStructValue value,
            string fieldName,
            string descriptionName)
        {
            if (value.TryGetInt64(fieldName, out var number))
            {
                description.Add(descriptionName, new LongElement(number));
            }
        }

        private static void AddNested(
            Description description,
            ITransportStructValue value,
            string fieldName,
            string descriptionName,
            Func<ITransportStructValue, Description> convert)
        {
            if (value.TryGetStruct(fieldName, out var nested))
            {
                description.Add(descriptionName, new DescriptionElement(convert(nested)));
            }
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
                new IntegerField("maxpackets", false, new IntegerType(int.MinValue, int.MaxValue)),
                new IntegerField("maxbytes", false, new IntegerType(int.MinValue, int.MaxValue))
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
                new IntegerField("packetoverhead", false, new IntegerType(int.MinValue, int.MaxValue)),
                new IntegerField("cellsize", false, new IntegerType(0, uint.MaxValue)),
                new IntegerField("celloverhead", false, new IntegerType(int.MinValue, int.MaxValue)),
                new IntegerField("queuelimit", false, new IntegerType(int.MinValue, int.MaxValue)),
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
