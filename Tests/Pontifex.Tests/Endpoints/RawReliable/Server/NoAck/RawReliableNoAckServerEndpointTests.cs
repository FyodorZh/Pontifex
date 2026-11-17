using Actuarius.Memory;
using Pontifex.StopReasons;

namespace Pontifex.Tests.Endpoints;

/// <summary>
/// Black-box contract tests for <see cref="Pontifex.Raw.Reliable.NoAck.RawReliableNoAckServerEndpoint"/>,
/// exercised through the extended public + protected surface exposed by
/// <see cref="TestRawReliableNoAckServerEndpoint"/>. Covers the logical connection / disconnect notifications
/// the class adds on top of <see cref="Pontifex.Raw.Reliable.RawReliableServerEndpoint"/>. The watchdog time is
/// driven through the overridable <c>Now</c> seam, so the tests stay deterministic.
/// </summary>
[Category("Endpoints")]
public sealed class RawReliableNoAckServerEndpointTests
{
    private static IMemoryRental Memory => MemoryRental.Shared;

    private static readonly TimeSpan DisconnectTimeout = TimeSpan.FromSeconds(30);

    [TearDown]
    public void TearDown()
    {
        GC.Collect();
    }

    private static TestRawReliableNoAckServerEndpoint Create(RecordingRawReliableNoAckServerHandler handler)
    {
        return new TestRawReliableNoAckServerEndpoint(handler, disconnectTimeout: DisconnectTimeout);
    }

    private static UserIntention Reason(string source = "test")
    {
        return new UserIntention(source);
    }

    [Test]
    public void Connect_NotifiesHandler()
    {
        using var handler = new RecordingRawReliableNoAckServerHandler(Memory);
        using var ep = Create(handler);

        ep.ConnectAndComplete();

        Assert.That(handler.ConnectedCount, Is.EqualTo(1));
        Assert.That(handler.ConnectedEndPoint, Is.SameAs(ep));
        Assert.That(ep.IsConnected, Is.True);
    }

    [Test]
    public void PeriodicTick_WhenIdle_NotifiesDisconnectedThenStopped()
    {
        using var handler = new RecordingRawReliableNoAckServerHandler(Memory);
        using var ep = Create(handler);
        ep.ConnectAndComplete();

        ep.AdvanceNow(DisconnectTimeout + TimeSpan.FromSeconds(1));
        ep.PeriodicTick();
        ep.DrainAll();

        Assert.That(handler.DisconnectedCount, Is.EqualTo(1));
        Assert.That(handler.DisconnectedReason, Is.TypeOf<TimeOut>());

        ep.CompleteDisconnect();

        Assert.That(handler.StoppedCount, Is.EqualTo(1));
        Assert.That(handler.StoppedReason, Is.TypeOf<TimeOut>());
    }

    [Test]
    public void Stop_WhenConnected_NotifiesDisconnectedThenStopped()
    {
        using var handler = new RecordingRawReliableNoAckServerHandler(Memory);
        using var ep = Create(handler);
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
        using var handler = new RecordingRawReliableNoAckServerHandler(Memory);
        using var ep = Create(handler);
        var reason = Reason();

        ep.Start();
        ep.DrainAll();

        ep.Stop(reason, tryBeGentle: false);
        ep.DrainAll();

        ep.CompleteConnect();

        Assert.That(handler.ConnectedCount, Is.EqualTo(1));
        Assert.That(handler.DisconnectedCount, Is.EqualTo(1));
        Assert.That(handler.Events, Is.EqualTo(new[] { "connected", "disconnected" }));

        ep.CompleteDisconnect();

        Assert.That(handler.StoppedCount, Is.EqualTo(1));
        Assert.That(handler.StoppedReason, Is.SameAs(reason));
        Assert.That(handler.Events, Is.EqualTo(new[] { "connected", "disconnected", "stopped" }));
    }

    [Test]
    public void FailHard_WhenConnected_NotifiesDisconnectedThenStopped()
    {
        using var handler = new RecordingRawReliableNoAckServerHandler(Memory);
        using var ep = Create(handler);
        ep.ConnectAndComplete();
        var reason = Reason();

        ep.ExposedFailHard(reason);

        Assert.That(handler.DisconnectedCount, Is.EqualTo(1));
        Assert.That(handler.DisconnectedReason, Is.SameAs(reason));
        Assert.That(handler.StoppedCount, Is.EqualTo(1));
        Assert.That(handler.StoppedReason, Is.SameAs(reason));
        Assert.That(ep.IsConnected, Is.False);
    }

    [Test]
    public void FailHard_WhenConnecting_NotifiesStoppedOnly()
    {
        using var handler = new RecordingRawReliableNoAckServerHandler(Memory);
        using var ep = Create(handler);
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
        using var handler = new RecordingRawReliableNoAckServerHandler(Memory);
        using var ep = Create(handler);
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
        using var handler = new RecordingRawReliableNoAckServerHandler(Memory)
        {
            ThrowOnConnected = true,
        };
        using var ep = Create(handler);

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
