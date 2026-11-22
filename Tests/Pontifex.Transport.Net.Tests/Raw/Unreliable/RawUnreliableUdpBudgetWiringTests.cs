using System.Net;
using Actuarius.Memory;
using NUnit.Framework;
using Pontifex.Raw.Unreliable.Udp;
using Scriba;

namespace Pontifex.Tests.Raw;

/// <summary>
/// Pins the budget constants a concrete unreliable UDP leaf wires into the transport limits hook.
/// </summary>
/// <remarks>
/// <para>
/// The carrier constants live in the leaf's <c>*Info</c> class and are the single source of truth. This loose
/// test asserts the overridable limits hook, so a wrong queue capacity or a mismatched Info pair cannot escape.
/// It does not bind a socket.
/// </para>
/// <para>
/// The absolute anchors pin the plan §6.1 starting caps: 490 × 100 = 49,000 bytes and 100 messages. Changing
/// a constant is a deliberate calibration change and must update this test; a silent constant drift is a test
/// failure, not a calibration change.
/// </para>
/// </remarks>
public sealed class RawUnreliableUdpBudgetWiringTests
{
    [Test]
    public void InfoConstants_PinThePlanStartingCaps()
    {
        Assert.Multiple(() =>
        {
            Assert.That(RawUnreliableUdpInfo.QueueCapacity, Is.EqualTo(100));
            Assert.That(RawUnreliableUdpInfo.SendBudgetMessages, Is.EqualTo(100));
            Assert.That(RawUnreliableUdpInfo.SendBudgetBytes, Is.EqualTo(49_000L));
        });
    }

    [Test]
    public void UdpClientAndServer_DeriveTheInfoBudgetCaps()
    {
        var logger = new Logger([]);
        var client = new RawUnreliableUdpClient(IPAddress.Loopback, 0, logger, MemoryRental.Shared);
        var server = new RawUnreliableUdpServer(IPAddress.Loopback, 0, logger, MemoryRental.Shared);

        Assert.Multiple(() =>
        {
            Assert.That(
                RawUnreliableUdpInfo.SendBudgetBytes,
                Is.EqualTo((long)RawUnreliableUdpInfo.MessageMaxByteSize *
                    RawUnreliableUdpInfo.QueueCapacity));
            Assert.That(
                RawUnreliableUdpInfo.SendBudgetMessages,
                Is.EqualTo(RawUnreliableUdpInfo.QueueCapacity));

            Assert.That(client.ExposedSendBudgetBytes,
                Is.EqualTo(RawUnreliableUdpInfo.SendBudgetBytes));
            Assert.That(client.ExposedSendBudgetMessages,
                Is.EqualTo(RawUnreliableUdpInfo.SendBudgetMessages));
            Assert.That(server.ExposedSendBudgetBytes,
                Is.EqualTo(RawUnreliableUdpInfo.SendBudgetBytes));
            Assert.That(server.ExposedSendBudgetMessages,
                Is.EqualTo(RawUnreliableUdpInfo.SendBudgetMessages));
        });
    }
}
