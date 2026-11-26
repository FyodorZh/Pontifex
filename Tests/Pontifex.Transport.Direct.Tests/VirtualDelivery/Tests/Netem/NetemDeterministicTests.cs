using System;
using Actuarius.Memory;
using Pontifex.Utils;
using Pontifex.VirtualDelivery.Netem;

namespace Pontifex.Tests.VirtualDelivery;

/// <summary>
/// Deterministic netem tests driven by the manual clock and pump seam. No test waits on wall-clock time or a
/// dequeue thread.
/// </summary>
[TestFixture]
[Category("Direct.VirtualDelivery")]
public sealed class NetemDeterministicTests
{
    [Test]
    public void MonotonicTime_ConvertsLargeTicksWithoutOverflow()
    {
        foreach (long frequency in new[] { 1_000_000_000L, 10_000_000L })
        {
            long ticks = 3_600_000_000_000L;
            long ns = MonotonicTime.ToNanoseconds(ticks, frequency);

            Assert.That(ns, Is.GreaterThan(0));
            Assert.That(ns, Is.EqualTo(ticks / frequency * 1_000_000_000L
                + ticks % frequency * 1_000_000_000L / frequency));
        }
    }

    [Test]
    public void MonotonicTime_IsNonDecreasing()
    {
        long first = MonotonicTime.NowNanoseconds();
        long second = MonotonicTime.NowNanoseconds();

        Assert.That(second, Is.GreaterThanOrEqualTo(first));
    }

    [Test]
    public void ManualPump_DeliversALatentMessageOnlyAfterTheDelay()
    {
        long now = 1_000;
        var delivery = Create(new NetemConfig { LatencyNs = 100 }, () => now);
        UnionDataList message = Message(1);

        delivery.Deliver(message);
        delivery.PumpDue(1_050);
        Assert.That(message.IsAlive, Is.True, "the message must still be queued before its due time");

        delivery.PumpDue(1_100);
        Assert.That(message.IsAlive, Is.False, "the pump must deliver the due message");
    }

    [Test]
    public void ManualPump_DuplicateProbabilityDeliversEveryCopy()
    {
        long now = 1_000;
        var delivery = Create(new NetemConfig
        {
            DuplicateProbability = uint.MaxValue,
            Correlation = new CorrelationParams(0, 0, 0, 0)
        }, () => now);
        int delivered = 0;
        delivery.Delivered += m =>
        {
            delivered++;
            m.Release();
        };
        UnionDataList message = Message(1);

        delivery.Deliver(message);
        delivery.PumpDue(now);
        delivery.PumpDue(now);

        Assert.That(delivered, Is.EqualTo(2));
    }

    [Test]
    public void QueueLimit_ClampsDuplicationToTheAvailableRoom()
    {
        long now = 1_000;
        var delivery = Create(new NetemConfig
        {
            QueueLimit = 1,
            DuplicateProbability = uint.MaxValue,
            Correlation = new CorrelationParams(0, 0, 0, 0)
        }, () => now);

        UnionDataList first = Message(1);
        UnionDataList second = Message(2);
        delivery.Deliver(first);
        delivery.Deliver(second);

        Assert.Multiple(() =>
        {
            Assert.That(second.IsAlive, Is.False, "no room means the next packet is released");
        });

        int delivered = 0;
        delivery.Delivered += m =>
        {
            delivered++;
            m.Release();
        };
        for (int i = 0; i < 10; i++)
        {
            delivery.PumpDue(now);
        }

        Assert.That(delivered, Is.EqualTo(1), "duplication must never exceed QueueLimit");
    }

    [Test]
    public void ManualPump_RateLimiterPacesConsecutiveMessages()
    {
        long now = 1_000;
        var delivery = Create(new NetemConfig
        {
            RateBytesPerSec = 1_000,
            PacketOverhead = 0
        }, () => now);
        int delivered = 0;
        delivery.Delivered += m =>
        {
            delivered++;
            m.Release();
        };

        delivery.Deliver(Message(1));
        delivery.PumpDue(now);
        Assert.That(delivered, Is.Zero, "the first paced message waits for its byte time");

        delivery.PumpDue(now + 10_000_000);
        Assert.That(delivered, Is.EqualTo(1), "the first paced message is due after its byte time");

        delivery.Deliver(Message(2));
        delivery.PumpDue(now + 10_000_000);
        Assert.That(delivered, Is.EqualTo(1), "the second message waits for the pacing bucket");

        delivery.PumpDue(now + 20_000_000);
        Assert.That(delivered, Is.EqualTo(2));
    }

    [Test]
    public void ManualPump_SlotWindowClosesAfterThePacketBudget()
    {
        long now = 1_000;
        var delivery = Create(new NetemConfig
        {
            Slot = new SlotConfig(1_000_000_000, 1_000_000_000, 0, 0, 1, int.MaxValue)
        }, () => now);
        int delivered = 0;
        delivery.Delivered += m =>
        {
            delivered++;
            m.Release();
        };

        delivery.Deliver(Message(1));
        delivery.PumpDue(now);
        Assert.That(delivered, Is.EqualTo(1));

        delivery.Deliver(Message(2));
        delivery.PumpDue(now);
        Assert.That(delivered, Is.EqualTo(1), "the slot closes after its packet budget");

        delivery.PumpDue(now + 1_000_000_000);
        Assert.That(delivered, Is.EqualTo(2));
    }

    [Test]
    public void Dispose_WithQueuedMessages_ReleasesThem()
    {
        var delivery = Create(new NetemConfig { LatencyNs = 1_000_000 }, () => 1_000);
        UnionDataList message = Message(1);
        delivery.Deliver(message);

        delivery.Dispose();

        Assert.That(message.IsAlive, Is.False);
    }

    [Test]
    public void ClearAndDispose_AreRepeatSafe()
    {
        var delivery = Create(new NetemConfig(), () => 1_000);
        delivery.Clear();
        delivery.Clear();
        delivery.Dispose();
        delivery.Dispose();

        Assert.DoesNotThrow(delivery.Clear);
    }

    [Test]
    public void Validate_RejectsEveryNegativeField()
    {
        Assert.Multiple(() =>
        {
            Assert.That(() => new NetemConfig { LatencyNs = -1 }.Validate(), Throws.InstanceOf<ArgumentOutOfRangeException>());
            Assert.That(() => new NetemConfig { JitterNs = -1 }.Validate(), Throws.InstanceOf<ArgumentOutOfRangeException>());
            Assert.That(() => new NetemConfig { PacketOverhead = -1 }.Validate(), Throws.InstanceOf<ArgumentOutOfRangeException>());
            Assert.That(() => new NetemConfig { CellOverhead = -1 }.Validate(), Throws.InstanceOf<ArgumentOutOfRangeException>());
            Assert.That(() => new NetemConfig { QueueLimit = -1 }.Validate(), Throws.InstanceOf<ArgumentOutOfRangeException>());
            Assert.That(() => new NetemConfig
            {
                Slot = new SlotConfig(-1, 0, 0, 0, 0, 0)
            }.Validate(), Throws.InstanceOf<ArgumentOutOfRangeException>());
            Assert.That(() => new NetemConfig
            {
                Slot = new SlotConfig(0, -1, 0, 0, 0, 0)
            }.Validate(), Throws.InstanceOf<ArgumentOutOfRangeException>());
            Assert.That(() => new NetemConfig
            {
                Slot = new SlotConfig(0, 0, -1, 0, 0, 0)
            }.Validate(), Throws.InstanceOf<ArgumentOutOfRangeException>());
            Assert.That(() => new NetemConfig
            {
                Slot = new SlotConfig(0, 0, 0, -1, 0, 0)
            }.Validate(), Throws.InstanceOf<ArgumentOutOfRangeException>());
            Assert.That(() => new NetemConfig
            {
                Slot = new SlotConfig(0, 0, 0, 0, -1, 0)
            }.Validate(), Throws.InstanceOf<ArgumentOutOfRangeException>());
            Assert.That(() => new NetemConfig
            {
                Slot = new SlotConfig(0, 0, 0, 0, 0, -1)
            }.Validate(), Throws.InstanceOf<ArgumentOutOfRangeException>());
        });
    }

    [Test]
    public void LossModels_ValidateTheirParameterSets()
    {
        Assert.Multiple(() =>
        {
            Assert.That(() => new NetemConfig
            {
                LossModel = LossModelKind.FourState,
                FourState = new FourStateParams(1, 1, 1, 1, 1)
            }.Validate(), Throws.Nothing);
            Assert.That(() => new NetemDeliverySystem(new NetemConfig
            {
                LossModel = LossModelKind.FourState
            }, MemoryRental.Shared.CollectablePool, MemoryRental.Shared.ByteArraysPool),
                Throws.ArgumentException);
            Assert.That(() => new NetemConfig
            {
                LossModel = LossModelKind.GilbertElliot,
                GilbertElliot = new GilbertElliotParams(1, 1, 1, 1)
            }.Validate(), Throws.Nothing);
            Assert.That(() => new NetemDeliverySystem(new NetemConfig
            {
                LossModel = LossModelKind.GilbertElliot
            }, MemoryRental.Shared.CollectablePool, MemoryRental.Shared.ByteArraysPool),
                Throws.ArgumentException);
            Assert.That(() => new NetemDeliverySystem(new NetemConfig
            {
                LossModel = (LossModelKind)999
            }, MemoryRental.Shared.CollectablePool, MemoryRental.Shared.ByteArraysPool),
                Throws.InstanceOf<ArgumentOutOfRangeException>());
        });
    }

    private static NetemDeliverySystem Create(NetemConfig config, Func<long> clock)
    {
        return new NetemDeliverySystem(config, MemoryRental.Shared.CollectablePool,
            MemoryRental.Shared.ByteArraysPool, clock, manualMode: true);
    }

    private static UnionDataList Message(int value)
    {
        var message = MemoryRental.Shared.CollectablePool.Acquire<UnionDataList>();
        message.PutLast(value);
        return message;
    }
}
