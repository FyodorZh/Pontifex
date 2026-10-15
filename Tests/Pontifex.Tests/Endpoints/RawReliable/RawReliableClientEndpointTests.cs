using Actuarius.Memory;
using Pontifex.StopReasons;
using Pontifex.Utils;

namespace Pontifex.Tests.Endpoints;

/// <summary>
/// Black-box contract tests for <see cref="Pontifex.Raw.Reliable.RawReliableClientEndpoint"/>, exercised
/// through the extended public + protected surface exposed by <see cref="TestRawReliableClientEndpoint"/>.
/// Covers the keep-alive configuration and pong handling the class adds on top of
/// <see cref="Pontifex.Raw.Reliable.RawReliableEndpoint"/>.
/// </summary>
[Category("Endpoints")]
public sealed class RawReliableClientEndpointTests
{
    private static IMemoryRental Memory => MemoryRental.Shared;

    private static readonly TimeSpan PingPeriod = TimeSpan.FromSeconds(30);

    private static readonly TimeSpan DisconnectTimeout = TimeSpan.FromSeconds(180);

    [TearDown]
    public void TearDown()
    {
        GC.Collect();
    }

    #region Keep-alive configuration

    [Test]
    public void Constructor_WithNonPositivePingPeriod_Throws()
    {
        using var handler = new RecordingRawReliableClientHandler(Memory);

        Assert.Throws<InvalidOperationException>(() => new TestRawReliableClientEndpoint(
            handler,
            pingPeriod: TimeSpan.Zero,
            disconnectTimeout: TimeSpan.FromSeconds(1)));
    }

    [Test]
    public void Constructor_WithDisconnectTimeoutLessThanTwicePingPeriod_Throws()
    {
        using var handler = new RecordingRawReliableClientHandler(Memory);

        Assert.Throws<InvalidOperationException>(() => new TestRawReliableClientEndpoint(
            handler,
            pingPeriod: TimeSpan.FromSeconds(10),
            disconnectTimeout: TimeSpan.FromSeconds(15)));
    }

    [Test]
    public void Constructor_WithValidPingOptions_DoesNotThrow()
    {
        using var handler = new RecordingRawReliableClientHandler(Memory);

        Assert.DoesNotThrow(() =>
        {
            using var ep = new TestRawReliableClientEndpoint(
                handler,
                pingPeriod: TimeSpan.FromSeconds(30),
                disconnectTimeout: TimeSpan.FromSeconds(180));
        });
    }

    #endregion

    #region Pong handling

    [Test]
    public void ProcessInbound_PongFrame_IsHandledAndDoesNotStop()
    {
        using var handler = new RecordingRawReliableClientHandler(Memory);
        using var ep = new TestRawReliableClientEndpoint(handler);
        ep.ConnectAndComplete();
        UnionDataList pong = TestRawReliableClientEndpoint.Frame(Memory, TestRawReliableClientEndpoint.PongFrame);

        ep.ExposedProcessInbound(pong);
        ep.DrainAll();

        Assert.That(pong.IsAlive, Is.False);
        Assert.That(handler.ReceivedCount, Is.Zero);
        Assert.That(handler.StoppedCount, Is.Zero);
        Assert.That(ep.BeginDisconnectCalls, Is.Zero);
        Assert.That(ep.IsLowLevelConnected, Is.True);
    }

    #endregion

    #region Keep-alive loop

    [Test]
    public void KeepAliveTick_WhenPingPeriodElapsed_SendsPingAndContinues()
    {
        using var handler = new RecordingRawReliableClientHandler(Memory);
        using var ep = new TestRawReliableClientEndpoint(
            handler, pingPeriod: PingPeriod, disconnectTimeout: DisconnectTimeout);
        ep.ConnectAndComplete();

        bool keepRunning = ep.ExposedKeepAliveTick();
        ep.DrainAll();

        Assert.That(keepRunning, Is.True);
        Assert.That(ep.CapturedSends.Count, Is.EqualTo(1));
        Assert.That(ep.CapturedSends[0].Elements[0].Alias.ByteValue, Is.EqualTo(TestRawReliableClientEndpoint.PingFrame));
        Assert.That(ep.BeginDisconnectCalls, Is.Zero);
        Assert.That(ep.IsLowLevelConnected, Is.True);
    }

    [Test]
    public void KeepAliveTick_WithinPingPeriod_DoesNotSendAgain()
    {
        using var handler = new RecordingRawReliableClientHandler(Memory);
        using var ep = new TestRawReliableClientEndpoint(
            handler, pingPeriod: PingPeriod, disconnectTimeout: DisconnectTimeout);
        ep.ConnectAndComplete();
        ep.ExposedKeepAliveTick();
        ep.DrainAll();

        ep.AdvanceNow(PingPeriod - TimeSpan.FromSeconds(1));
        bool keepRunning = ep.ExposedKeepAliveTick();
        ep.DrainAll();

        Assert.That(keepRunning, Is.True);
        Assert.That(ep.CapturedSends.Count, Is.EqualTo(1));
    }

    [Test]
    public void KeepAliveTick_WhenPongTimeoutElapsed_StopsWithTimeOut()
    {
        using var handler = new RecordingRawReliableClientHandler(Memory);
        using var ep = new TestRawReliableClientEndpoint(
            handler, pingPeriod: PingPeriod, disconnectTimeout: DisconnectTimeout);
        ep.ConnectAndComplete();

        ep.AdvanceNow(DisconnectTimeout + TimeSpan.FromSeconds(1));
        bool keepRunning = ep.ExposedKeepAliveTick();
        ep.DrainAll();

        Assert.That(keepRunning, Is.False);
        Assert.That(ep.CapturedSends, Is.Empty);
        Assert.That(ep.BeginDisconnectCalls, Is.EqualTo(1));
        Assert.That(ep.LastDisconnectReason, Is.TypeOf<TimeOut>());

        ep.CompleteDisconnect();

        Assert.That(handler.StoppedReason, Is.TypeOf<TimeOut>());
    }

    [Test]
    public void KeepAliveTick_WhenPingCannotBeAdmitted_StopsWithTextFail()
    {
        using var handler = new RecordingRawReliableClientHandler(Memory);
        using var ep = new TestRawReliableClientEndpoint(
            handler, bufferCapacityBytes: 1, pingPeriod: PingPeriod, disconnectTimeout: DisconnectTimeout);
        ep.ConnectAndComplete();

        bool keepRunning = ep.ExposedKeepAliveTick();
        ep.DrainAll();

        Assert.That(keepRunning, Is.False);
        Assert.That(ep.CapturedSends, Is.Empty);
        Assert.That(ep.BeginDisconnectCalls, Is.EqualTo(1));
        Assert.That(ep.LastDisconnectReason, Is.TypeOf<TextFail>());

        ep.CompleteDisconnect();

        Assert.That(handler.StoppedReason, Is.TypeOf<TextFail>());
    }

    #endregion
}
