using Actuarius.Memory;
using Pontifex.StopReasons;
using Pontifex.Utils;

namespace Pontifex.Tests.Endpoints;

/// <summary>
/// Black-box contract tests for <see cref="Pontifex.Raw.Reliable.RawReliableServerEndpoint"/>, exercised
/// through the extended public + protected surface exposed by <see cref="TestRawReliableServerEndpoint"/>.
/// Covers the idle-session watchdog and ping answering the class adds on top of
/// <see cref="Pontifex.Raw.Reliable.RawReliableEndpoint"/>. The watchdog time is driven through the
/// overridable <c>Now</c> seam, so the tests stay deterministic.
/// </summary>
[Category("Endpoints")]
public sealed class RawReliableServerEndpointTests
{
    private static IMemoryRental Memory => MemoryRental.Shared;

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    [TearDown]
    public void TearDown()
    {
        GC.Collect();
    }

    private static TestRawReliableServerEndpoint Create(
        RecordingRawReliableServerHandler handler,
        TimeSpan? disconnectTimeout = null,
        int max = 1024,
        long capacity = 1024 * 1024)
    {
        return new TestRawReliableServerEndpoint(
            handler,
            messageMaxByteSize: max,
            bufferCapacityBytes: capacity,
            disconnectTimeout: disconnectTimeout ?? Timeout);
    }

    #region Constructor

    [Test]
    public void Constructor_WithNonPositiveDisconnectTimeout_Throws()
    {
        using var handler = new RecordingRawReliableServerHandler(Memory);

        Assert.Throws<ArgumentException>(() => Create(handler, disconnectTimeout: TimeSpan.Zero));
    }

    #endregion

    #region Ping answering

    [Test]
    public void ProcessInbound_PingFrame_AnswersWithPongFrame()
    {
        using var handler = new RecordingRawReliableServerHandler(Memory);
        using var ep = Create(handler);
        ep.ConnectAndComplete();
        UnionDataList ping = TestRawReliableServerEndpoint.Frame(Memory, TestRawReliableServerEndpoint.PingFrame);

        ep.ExposedProcessInbound(ping);
        ep.DrainAll();

        Assert.That(handler.ReceivedCount, Is.Zero);
        Assert.That(ep.CapturedSends.Count, Is.EqualTo(1));
        Assert.That(ep.CapturedSends[0].Elements[0].Alias.ByteValue, Is.EqualTo(TestRawReliableServerEndpoint.PongFrame));
        Assert.That(ep.BeginDisconnectCalls, Is.Zero);
        Assert.That(ep.IsLowLevelConnected, Is.True);
    }

    [Test]
    public void ProcessInbound_PingFrame_WhenPongCannotBeAdmitted_StopsWithTextFail()
    {
        using var handler = new RecordingRawReliableServerHandler(Memory);
        using var ep = Create(handler, capacity: 1);
        ep.ConnectAndComplete();
        UnionDataList ping = TestRawReliableServerEndpoint.Frame(Memory, TestRawReliableServerEndpoint.PingFrame);

        ep.ExposedProcessInbound(ping);
        ep.DrainAll();

        Assert.That(ep.CapturedSends, Is.Empty);
        Assert.That(ep.BeginDisconnectCalls, Is.EqualTo(1));
        Assert.That(ep.LastDisconnectReason, Is.TypeOf<TextFail>());

        ep.CompleteDisconnect();

        Assert.That(handler.StoppedReason, Is.TypeOf<TextFail>());
    }

    #endregion

    #region Idle-session watchdog

    [Test]
    public void PeriodicTick_WhenNotConnected_DoesNothing()
    {
        using var handler = new RecordingRawReliableServerHandler(Memory);
        using var ep = Create(handler);

        ep.PeriodicTick();
        ep.DrainAll();

        Assert.That(ep.BeginDisconnectCalls, Is.Zero);
        Assert.That(handler.StoppedCount, Is.Zero);
    }

    [Test]
    public void PeriodicTick_WithinDisconnectTimeout_DoesNothing()
    {
        using var handler = new RecordingRawReliableServerHandler(Memory);
        using var ep = Create(handler);
        ep.ConnectAndComplete();

        ep.AdvanceNow(Timeout - TimeSpan.FromSeconds(1));
        ep.PeriodicTick();
        ep.DrainAll();

        Assert.That(ep.BeginDisconnectCalls, Is.Zero);
        Assert.That(handler.StoppedCount, Is.Zero);
    }

    [Test]
    public void PeriodicTick_AfterDisconnectTimeout_StopsWithTimeOut()
    {
        using var handler = new RecordingRawReliableServerHandler(Memory);
        using var ep = Create(handler);
        ep.ConnectAndComplete();

        ep.AdvanceNow(Timeout + TimeSpan.FromSeconds(1));
        ep.PeriodicTick();
        ep.DrainAll();

        Assert.That(ep.BeginDisconnectCalls, Is.EqualTo(1));
        Assert.That(ep.LastDisconnectReason, Is.TypeOf<TimeOut>());

        ep.CompleteDisconnect();

        Assert.That(handler.StoppedReason, Is.TypeOf<TimeOut>());
    }

    [Test]
    public void PeriodicTick_AfterInboundWithinTimeout_DoesNothing()
    {
        using var handler = new RecordingRawReliableServerHandler(Memory);
        using var ep = Create(handler);
        ep.ConnectAndComplete();

        // Move close to the deadline, then let an inbound frame refresh the watchdog.
        ep.AdvanceNow(Timeout - TimeSpan.FromSeconds(10));
        UnionDataList payload = TestRawReliableServerEndpoint.Frame(Memory, TestRawReliableServerEndpoint.PayloadFrame, 1);
        ep.ExposedProcessInbound(payload);
        ep.DrainAll();

        // Past the original deadline, but within the timeout of the refreshed timestamp.
        ep.AdvanceNow(TimeSpan.FromSeconds(20));
        ep.PeriodicTick();
        ep.DrainAll();

        Assert.That(ep.BeginDisconnectCalls, Is.Zero);
        Assert.That(handler.StoppedCount, Is.Zero);
    }

    #endregion
}
