using System.Net;
using Actuarius.Memory;
using NUnit.Framework;
using Pontifex.Raw.Unreliable.Ack.Udp;
using Pontifex.Raw.Unreliable.NoAck.Udp;
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
            Assert.That(RawUnreliableAckUdpInfo.QueueCapacity, Is.EqualTo(100));
            Assert.That(RawUnreliableAckUdpInfo.SendBudgetMessages, Is.EqualTo(100));
            Assert.That(RawUnreliableAckUdpInfo.SendBudgetBytes, Is.EqualTo(49_000L));

            Assert.That(RawUnreliableNoAckUdpInfo.QueueCapacity, Is.EqualTo(100));
            Assert.That(RawUnreliableNoAckUdpInfo.SendBudgetMessages, Is.EqualTo(100));
            Assert.That(RawUnreliableNoAckUdpInfo.SendBudgetBytes, Is.EqualTo(49_000L));
        });
    }

    [Test]
    public void AckUdpClientAndServer_DeriveTheInfoBudgetCaps()
    {
        var logger = new Logger([]);
        var client = new RawUnreliableAckUdpClient(IPAddress.Loopback, 0, logger, MemoryRental.Shared);
        var server = new RawUnreliableAckUdpServer(IPAddress.Loopback, 0, logger, MemoryRental.Shared);

        Assert.Multiple(() =>
        {
            Assert.That(
                RawUnreliableAckUdpInfo.SendBudgetBytes,
                Is.EqualTo((long)RawUnreliableAckUdpInfo.MessageMaxByteSize *
                    RawUnreliableAckUdpInfo.QueueCapacity));
            Assert.That(
                RawUnreliableAckUdpInfo.SendBudgetMessages,
                Is.EqualTo(RawUnreliableAckUdpInfo.QueueCapacity));

            Assert.That(client.ExposedSendBudgetBytes,
                Is.EqualTo(RawUnreliableAckUdpInfo.SendBudgetBytes));
            Assert.That(client.ExposedSendBudgetMessages,
                Is.EqualTo(RawUnreliableAckUdpInfo.SendBudgetMessages));
            Assert.That(server.ExposedSendBudgetBytes,
                Is.EqualTo(RawUnreliableAckUdpInfo.SendBudgetBytes));
            Assert.That(server.ExposedSendBudgetMessages,
                Is.EqualTo(RawUnreliableAckUdpInfo.SendBudgetMessages));
        });
    }
}
