using Actuarius.Memory;
using NUnit.Framework;
using Pontifex.Raw.Unreliable.Direct;
using Scriba;

namespace Pontifex.Tests.Raw.RawUnreliableDirect;

/// <summary>
/// Pins the budget constants the unreliable Direct leaf wires into the transport limits hook.
/// </summary>
/// <remarks>
/// <para>
/// The carrier constants live in the leaf's <c>*Info</c> class and are the single source of truth. This
/// test asserts the derived transport values, so a wrong queue capacity or a mismatched Info pair cannot
/// escape. It does not start a transport.
/// </para>
/// <para>
/// The absolute anchors pin the plan §6.1 starting caps: 1 MiB × 100 = 104,857,600 bytes and 100 messages.
/// Changing a constant is a deliberate calibration change and must update this test; a silent constant drift
/// is a test failure, not a calibration change.
/// </para>
/// </remarks>
[TestFixture]
[Category("Direct.RawUnreliable")]
public sealed class RawUnreliableDirectBudgetWiringTests
{
    [Test]
    public void InfoConstants_PinThePlanStartingCaps()
    {
        Assert.Multiple(() =>
        {
            Assert.That(RawUnreliableDirectInfo.QueueCapacity, Is.EqualTo(100));
            Assert.That(RawUnreliableDirectInfo.SendBudgetMessages, Is.EqualTo(100));
            Assert.That(RawUnreliableDirectInfo.SendBudgetBytes, Is.EqualTo(104_857_600L));
        });
    }

    [Test]
    public void DirectClientAndServer_DeriveTheInfoBudgetCaps()
    {
        var logger = new Logger([]);
        var client = new RawUnreliableDirectClientTransport("budget-wiring", logger, MemoryRental.Shared);
        var server = new RawUnreliableDirectServerTransport("budget-wiring", logger, MemoryRental.Shared);

        Assert.Multiple(() =>
        {
            Assert.That(
                RawUnreliableDirectInfo.SendBudgetBytes,
                Is.EqualTo((long)RawUnreliableDirectInfo.MessageMaxByteSize *
                    RawUnreliableDirectInfo.QueueCapacity));
            Assert.That(
                RawUnreliableDirectInfo.SendBudgetMessages,
                Is.EqualTo(RawUnreliableDirectInfo.QueueCapacity));

            Assert.That(client.ExposedSendBudgetBytes,
                Is.EqualTo(RawUnreliableDirectInfo.SendBudgetBytes));
            Assert.That(client.ExposedSendBudgetMessages,
                Is.EqualTo(RawUnreliableDirectInfo.SendBudgetMessages));
            Assert.That(server.ExposedSendBudgetBytes,
                Is.EqualTo(RawUnreliableDirectInfo.SendBudgetBytes));
            Assert.That(server.ExposedSendBudgetMessages,
                Is.EqualTo(RawUnreliableDirectInfo.SendBudgetMessages));
        });
    }
}
