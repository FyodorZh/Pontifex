using Actuarius.Memory;
using Pontifex.StopReasons;

namespace Pontifex.Tests.Endpoints;

/// <summary>
/// Black-box contract tests for <see cref="Pontifex.Raw.Reliable.NoAck.RawReliableNoAckClientEndpoint"/>,
/// exercised through the extended public + protected surface exposed by
/// <see cref="TestRawReliableNoAckClientEndpoint"/>. Covers the logical connection / disconnect notifications
/// the class adds on top of <see cref="Pontifex.Raw.Reliable.RawReliableClientEndpoint"/>.
/// </summary>
[Category("Endpoints")]
public sealed class RawReliableNoAckClientEndpointTests
{
    private static IMemoryRental Memory => MemoryRental.Shared;

    [TearDown]
    public void TearDown()
    {
        GC.Collect();
    }

    private static UserIntention Reason(string source = "test")
    {
        return new UserIntention(source);
    }

    [Test]
    public void Connect_NotifiesHandlerAndSetsIsConnected()
    {
        using var handler = new RecordingRawReliableNoAckClientHandler(Memory);
        using var ep = new TestRawReliableNoAckClientEndpoint(handler);

        ep.ConnectAndComplete();

        Assert.That(handler.ConnectedCount, Is.EqualTo(1));
        Assert.That(handler.ConnectedEndPoint, Is.SameAs(ep));
        Assert.That(ep.IsConnected, Is.True);
    }

    [Test]
    public void Stop_WhenConnected_NotifiesDisconnectedThenStopped()
    {
        using var handler = new RecordingRawReliableNoAckClientHandler(Memory);
        using var ep = new TestRawReliableNoAckClientEndpoint(handler);
        ep.ConnectAndComplete();
        var reason = Reason();

        ep.Stop(reason, tryBeGentle: false);
        ep.DrainAll();

        Assert.That(handler.DisconnectedCount, Is.EqualTo(1));
        Assert.That(handler.DisconnectedReason, Is.SameAs(reason));
        Assert.That(ep.IsConnected, Is.False);

        ep.CompleteDisconnect();

        Assert.That(handler.StoppedCount, Is.EqualTo(1));
        Assert.That(handler.StoppedReason, Is.SameAs(reason));
        Assert.That(handler.Events, Is.EqualTo(new[] { "connected", "disconnected", "stopped" }));
    }

    [Test]
    public void Stop_WhileConnecting_ReportsConnectionThenTearsDown()
    {
        using var handler = new RecordingRawReliableNoAckClientHandler(Memory);
        using var ep = new TestRawReliableNoAckClientEndpoint(handler);
        var reason = Reason();

        ep.Start();
        ep.DrainAll();

        ep.Stop(reason, tryBeGentle: false);
        ep.DrainAll();

        ep.CompleteConnect();

        Assert.That(handler.ConnectedCount, Is.EqualTo(1));
        Assert.That(handler.DisconnectedCount, Is.EqualTo(1));

        ep.CompleteDisconnect();

        Assert.That(handler.StoppedCount, Is.EqualTo(1));
        Assert.That(handler.StoppedReason, Is.SameAs(reason));
    }

    [Test]
    public void FailHard_WhenConnected_NotifiesDisconnectedThenStopped()
    {
        using var handler = new RecordingRawReliableNoAckClientHandler(Memory);
        using var ep = new TestRawReliableNoAckClientEndpoint(handler);
        ep.ConnectAndComplete();
        var reason = Reason();

        ep.ExposedFailHard(reason);

        Assert.That(handler.DisconnectedCount, Is.EqualTo(1));
        Assert.That(handler.DisconnectedReason, Is.SameAs(reason));
        Assert.That(handler.StoppedCount, Is.EqualTo(1));
        Assert.That(handler.StoppedReason, Is.SameAs(reason));
        Assert.That(handler.Events, Is.EqualTo(new[] { "connected", "disconnected", "stopped" }));
        Assert.That(ep.IsConnected, Is.False);
    }

    [Test]
    public void FailHard_WhenConnecting_NotifiesStoppedOnly()
    {
        using var handler = new RecordingRawReliableNoAckClientHandler(Memory);
        using var ep = new TestRawReliableNoAckClientEndpoint(handler);
        var reason = Reason();

        ep.Start();
        ep.DrainAll();

        ep.ExposedFailHard(reason);

        Assert.That(handler.DisconnectedCount, Is.Zero);
        Assert.That(handler.StoppedCount, Is.EqualTo(1));
        Assert.That(handler.StoppedReason, Is.SameAs(reason));
    }

    [Test]
    public void Disconnect_WhenConnected_MakesIsConnectedFalseDuringOnDisconnected()
    {
        using var handler = new RecordingRawReliableNoAckClientHandler(Memory);
        using var ep = new TestRawReliableNoAckClientEndpoint(handler);
        ep.ConnectAndComplete();
        handler.IsConnectedProbe = () => ep.IsConnected;

        ep.Stop(Reason(), tryBeGentle: false);
        ep.DrainAll();
        ep.CompleteDisconnect();

        Assert.That(handler.IsConnectedDuringDisconnected, Is.False);
    }

    [Test]
    public void OnConnected_WhenHandlerThrows_StopsWithExceptionFail()
    {
        using var handler = new RecordingRawReliableNoAckClientHandler(Memory)
        {
            ThrowOnConnected = true,
        };
        using var ep = new TestRawReliableNoAckClientEndpoint(handler);

        ep.ConnectAndComplete();

        Assert.That(handler.ConnectedCount, Is.EqualTo(1));
        Assert.That(handler.DisconnectedCount, Is.EqualTo(1));
        Assert.That(handler.DisconnectedReason, Is.TypeOf<ExceptionFail>());
        Assert.That(ep.BeginDisconnectCalls, Is.EqualTo(1));

        ep.CompleteDisconnect();

        Assert.That(handler.StoppedCount, Is.EqualTo(1));
        Assert.That(handler.StoppedReason, Is.TypeOf<ExceptionFail>());
        Assert.That(ep.IsConnected, Is.False);
    }
}
