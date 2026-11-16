using Actuarius.Memory;
using NUnit.Framework;
using Pontifex.Raw.Unreliable.Ack.Direct;
using Pontifex.Raw.Unreliable.NoAck.Direct;
using Scriba;

namespace Pontifex.Tests.Raw;

/// <summary>
/// Pins the budget constants a concrete unreliable Direct leaf wires into the transport limits hook.
/// </summary>
/// <remarks>
/// <para>
/// The carrier constants live in the leaf's <c>*Info</c> class and are the single source of truth. This loose
/// test asserts the derived transport values, so a wrong queue capacity or a mismatched Info pair cannot
/// escape. It does not start a transport.
/// </para>
/// <para>
/// The absolute anchors pin the plan §6.1 starting caps: 1 MiB × 100 = 104,857,600 bytes and 100 messages.
/// Changing a constant is a deliberate calibration change and must update this test; a silent constant drift
/// is a test failure, not a calibration change.
/// </para>
/// </remarks>
public sealed class RawUnreliableDirectBudgetWiringTests
{
    [Test]
    public void InfoConstants_PinThePlanStartingCaps()
    {
        Assert.Multiple(() =>
        {
            Assert.That(RawUnreliableAckDirectInfo.QueueCapacity, Is.EqualTo(100));
            Assert.That(RawUnreliableAckDirectInfo.SendBudgetMessages, Is.EqualTo(100));
            Assert.That(RawUnreliableAckDirectInfo.SendBudgetBytes, Is.EqualTo(104_857_600L));

            Assert.That(RawUnreliableNoAckDirectInfo.QueueCapacity, Is.EqualTo(100));
            Assert.That(RawUnreliableNoAckDirectInfo.SendBudgetMessages, Is.EqualTo(100));
            Assert.That(RawUnreliableNoAckDirectInfo.SendBudgetBytes, Is.EqualTo(104_857_600L));
        });
    }

    [Test]
    public void AckDirectClientAndServer_DeriveTheInfoBudgetCaps()
    {
        var logger = new Logger([]);
        var client = new RawUnreliableAckDirectClient("budget-wiring", logger, MemoryRental.Shared);
        var server = new RawUnreliableAckDirectServer("budget-wiring", logger, MemoryRental.Shared);

        Assert.Multiple(() =>
        {
            Assert.That(
                RawUnreliableAckDirectInfo.SendBudgetBytes,
                Is.EqualTo((long)RawUnreliableAckDirectInfo.MessageMaxByteSize *
                    RawUnreliableAckDirectInfo.QueueCapacity));
            Assert.That(
                RawUnreliableAckDirectInfo.SendBudgetMessages,
                Is.EqualTo(RawUnreliableAckDirectInfo.QueueCapacity));

            Assert.That(client.ExposedSendBudgetBytes,
                Is.EqualTo(RawUnreliableAckDirectInfo.SendBudgetBytes));
            Assert.That(client.ExposedSendBudgetMessages,
                Is.EqualTo(RawUnreliableAckDirectInfo.SendBudgetMessages));
            Assert.That(server.ExposedSendBudgetBytes,
                Is.EqualTo(RawUnreliableAckDirectInfo.SendBudgetBytes));
            Assert.That(server.ExposedSendBudgetMessages,
                Is.EqualTo(RawUnreliableAckDirectInfo.SendBudgetMessages));
        });
    }
}
