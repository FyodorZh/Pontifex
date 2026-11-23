using Actuarius.Memory;
using Pontifex.Utils;
using Pontifex.VirtualDelivery.Netem;

namespace Pontifex.Tests.VirtualDelivery;

/// <summary>
/// Pins the impairment decisions of the netem pipeline that are decided synchronously in
/// <see cref="NetemDeliverySystem.Deliver"/>.
/// </summary>
[TestFixture]
[Category("Direct.VirtualDelivery")]
public sealed class NetemDeliverySystemTests
{
    [Test]
    public void SaturatedLossWithDuplication_DropsThePacket()
    {
        var config = new NetemConfig
        {
            // Duplication creates a second copy before the loss decision. A dropped packet must
            // stay dropped instead of surviving as the remaining copy.
            DuplicateProbability = uint.MaxValue,
            LossProbability = uint.MaxValue,
            LatencyNs = 3_600_000_000_000,
            Correlation = new CorrelationParams(0, 0, 0, 0)
        };

        var delivery = new NetemDeliverySystem(
            config,
            MemoryRental.Shared.CollectablePool,
            MemoryRental.Shared.ByteArraysPool);

        try
        {
            var message = MemoryRental.Shared.CollectablePool.Acquire<UnionDataList>();
            message.PutLast(1);

            delivery.Deliver(message);

            // A queued packet would still be alive: the base latency is one hour. A dropped packet
            // is released before Deliver returns.
            Assert.That(message.IsAlive, Is.False,
                "A duplicated packet survived a saturated loss decision.");
        }
        finally
        {
            delivery.Dispose();
        }
    }
}
