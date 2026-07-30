using System;
using Actuarius.Memory;
using Pontifex.Factory;
using Pontifex.VirtualDelivery.Netem;

namespace Pontifex.VirtualDelivery
{
    public static class DeliverySystemFactory
    {
        public static IDeliverySystem? Build(IDescription description,
            ICollectablePool collectablePool, IPool<IMultiRefByteArray, int> bytesPool)
        {
            var typeElement = description.Get("type");
            if (!typeElement.EvaluateAsString(out var type))
                return null;

            return type switch
            {
                "perfect" => BuildPerfect(),
                "netem" => BuildNetem(description, collectablePool, bytesPool),
                _ => null
            };
        }

        private static IDeliverySystem? BuildPerfect()
        {
            return new PerfectDeliverySystem();
        }

        private static IDeliverySystem? BuildNetem(IDescription description,
            ICollectablePool collectablePool, IPool<IMultiRefByteArray, int> bytesPool)
        {
            var config = ParseNetemConfig(description);
            if (config == null)
                return null;

            try
            {
                return new NetemDeliverySystem(config, collectablePool, bytesPool);
            }
            catch
            {
                return null;
            }
        }

        private static NetemConfig? ParseNetemConfig(IDescription description)
        {
            if (!TryGetLong(description, "latencyNs", out var latencyNs))
                return null;

            TryGetLong(description, "jitterNs", out var jitterNs);
            TryGetUInt32(description, "lossProbability", out var lossProbability);
            TryGetUInt32(description, "duplicateProbability", out var duplicateProbability);
            TryGetUInt32(description, "reorderProbability", out var reorderProbability);
            TryGetUInt32(description, "gap", out var gap);
            TryGetUInt64(description, "rateBytesPerSec", out var rateBytesPerSec);
            TryGetInt32(description, "packetOverhead", out var packetOverhead);
            TryGetUInt32(description, "cellSize", out var cellSize);
            TryGetInt32(description, "cellOverhead", out var cellOverhead);
            TryGetInt32(description, "queueLimit", out var queueLimit);

            LossModelKind? lossModel = null;
            if (TryGetString(description, "lossModel", out var lossModelStr))
            {
                lossModel = ParseLossModelKind(lossModelStr);
                if (lossModel == null)
                    return null;
            }

            CorrelationParams? correlation = null;
            if (TryGetDescription(description, "correlation", out var correlationDesc))
            {
                correlation = ParseCorrelation(correlationDesc!);
                if (correlation == null)
                    return null;
            }

            SlotConfig? slot = null;
            if (TryGetDescription(description, "slot", out var slotDesc))
            {
                slot = ParseSlotConfig(slotDesc!);
                if (slot == null)
                    return null;
            }

            DistributionTable? delayDistribution = null;
            if (TryGetDescription(description, "delayDistribution", out var distDesc))
            {
                delayDistribution = ParseDistributionTable(distDesc!);
                if (delayDistribution == null)
                    return null;
            }

            DistributionTable? slotDistribution = null;
            if (TryGetDescription(description, "slotDistribution", out var slotDistDesc))
            {
                slotDistribution = ParseDistributionTable(slotDistDesc!);
                if (slotDistribution == null)
                    return null;
            }

            FourStateParams? fourState = null;
            if (TryGetDescription(description, "fourState", out var fourStateDesc))
            {
                fourState = ParseFourStateParams(fourStateDesc!);
                if (fourState == null)
                    return null;
            }

            GilbertElliotParams? gilbertElliot = null;
            if (TryGetDescription(description, "gilbertElliot", out var geDesc))
            {
                gilbertElliot = ParseGilbertElliotParams(geDesc!);
                if (gilbertElliot == null)
                    return null;
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
                LossModel = lossModel ?? LossModelKind.Random,
                Correlation = correlation ?? default,
                Slot = slot ?? default,
                DelayDistribution = delayDistribution,
                SlotDistribution = slotDistribution,
                FourState = fourState,
                GilbertElliot = gilbertElliot
            };
        }

        private static LossModelKind? ParseLossModelKind(string value)
        {
            return value switch
            {
                "random" => LossModelKind.Random,
                "fourState" => LossModelKind.FourState,
                "gilbertElliot" => LossModelKind.GilbertElliot,
                _ => null
            };
        }

        private static CorrelationParams? ParseCorrelation(IDescription desc)
        {
            if (!TryGetUInt32(desc, "delayRho", out var delayRho))
                return null;
            if (!TryGetUInt32(desc, "lossRho", out var lossRho))
                return null;
            if (!TryGetUInt32(desc, "duplicateRho", out var duplicateRho))
                return null;
            if (!TryGetUInt32(desc, "reorderRho", out var reorderRho))
                return null;

            return new CorrelationParams(delayRho, lossRho, duplicateRho, reorderRho);
        }

        private static SlotConfig? ParseSlotConfig(IDescription desc)
        {
            if (!TryGetLong(desc, "minDelayNs", out var minDelayNs))
                return null;
            if (!TryGetLong(desc, "maxDelayNs", out var maxDelayNs))
                return null;
            if (!TryGetLong(desc, "distDelayNs", out var distDelayNs))
                return null;
            if (!TryGetLong(desc, "distJitterNs", out var distJitterNs))
                return null;
            if (!TryGetInt32(desc, "maxPackets", out var maxPackets))
                return null;
            if (!TryGetInt32(desc, "maxBytes", out var maxBytes))
                return null;

            return new SlotConfig(minDelayNs, maxDelayNs, distDelayNs, distJitterNs, maxPackets, maxBytes);
        }

        private static DistributionTable? ParseDistributionTable(IDescription desc)
        {
            var valuesElement = desc.Get("values");
            if (!valuesElement.EvaluateAsArray(out var values))
                return null;

            var shorts = new short[values.Count];
            for (int i = 0; i < values.Count; i++)
            {
                if (!values[i].EvaluateAsLong(out var v))
                    return null;
                shorts[i] = (short)v;
            }

            return new DistributionTable(shorts);
        }

        private static FourStateParams? ParseFourStateParams(IDescription desc)
        {
            if (!TryGetUInt32(desc, "p13", out var p13))
                return null;
            if (!TryGetUInt32(desc, "p31", out var p31))
                return null;
            if (!TryGetUInt32(desc, "p32", out var p32))
                return null;
            if (!TryGetUInt32(desc, "p14", out var p14))
                return null;
            if (!TryGetUInt32(desc, "p23", out var p23))
                return null;

            return new FourStateParams(p13, p31, p32, p14, p23);
        }

        private static GilbertElliotParams? ParseGilbertElliotParams(IDescription desc)
        {
            if (!TryGetUInt32(desc, "p", out var p))
                return null;
            if (!TryGetUInt32(desc, "r", out var r))
                return null;
            if (!TryGetUInt32(desc, "h", out var h))
                return null;
            if (!TryGetUInt32(desc, "k1", out var k1))
                return null;

            return new GilbertElliotParams(p, r, h, k1);
        }

        private static bool TryGetLong(IDescription desc, string name, out long value)
        {
            var element = desc.Get(name);
            return element.EvaluateAsLong(out value);
        }

        private static bool TryGetInt32(IDescription desc, string name, out int value)
        {
            var element = desc.Get(name);
            if (element.EvaluateAsLong(out var v))
            {
                value = (int)v;
                return true;
            }
            value = 0;
            return false;
        }

        private static bool TryGetUInt32(IDescription desc, string name, out uint value)
        {
            var element = desc.Get(name);
            if (element.EvaluateAsLong(out var v))
            {
                value = (uint)v;
                return true;
            }
            value = 0;
            return false;
        }

        private static bool TryGetUInt64(IDescription desc, string name, out ulong value)
        {
            var element = desc.Get(name);
            if (element.EvaluateAsLong(out var v))
            {
                value = (ulong)v;
                return true;
            }
            value = 0;
            return false;
        }

        private static bool TryGetString(IDescription desc, string name, out string value)
        {
            var element = desc.Get(name);
            return element.EvaluateAsString(out value);
        }

        private static bool TryGetDescription(IDescription desc, string name, out IDescription? value)
        {
            var element = desc.Get(name);
            return element.EvaluateAsDescription(out value);
        }
    }
}
