using System;
using System.Collections.Generic;
using System.Threading;
using Actuarius.Memory;
using Pontifex.Endpoints;
using Pontifex.RR.Direct;
using Pontifex.RR.Reliable;
using Pontifex.RR.Reliable.Direct;
using Pontifex.StopReasons;
using Pontifex.Tests.RR.RRReliableDirect.Harness;
using Pontifex.Tests.TestSupport;
using Scriba;

namespace Pontifex.Tests.RR.RRReliableDirect;

/// <summary>
/// Pins the lifecycle of the in-process RR reliable carrier: start and init rules, stop scheduling,
/// channel loss before and after a live session, and the no-failure pre-session path.
/// </summary>
[TestFixture]
[Category("Direct.RRReliable")]
public sealed class RRReliableDirectLifecycleTests
{
    private static readonly ILogger Logger = new Logger([]);
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly List<ITransport> _transports = new();

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        Logger.Dispose();
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = _transports.Count - 1; i >= 0; i--)
        {
            _transports[i].Stop();
        }

        _transports.Clear();
    }

    [Test]
    public void ClientStart_IsOneTime()
    {
        var client = new RRReliableDirectClient(
            new StringEndPoint(NewName()),
            TimeSpan.FromSeconds(3),
            Logger,
            MemoryRental.Shared,
            new ManualTimerFactory());
        _transports.Add(client);

        Assert.That(client.Start(_ => { }), Is.True);
        Assert.That(client.Start(_ => { }), Is.False);
        Assert.That(client.IsStarted, Is.True);
    }

    [Test]
    public void ServerInit_IsOneTimeAndRejectsNull()
    {
        using var handler = new RecordingReliableServerHandler(MemoryRental.Shared);
        var server = NewServer(NewName());

        Assert.That(() => server.Init(null!), Throws.TypeOf<ArgumentNullException>());
        Assert.That(server.Init(handler), Is.True);
        Assert.That(server.Init(handler), Is.False);
    }

    [Test]
    public void ServerStartWithoutInit_ReturnsFalseAndInvalidates()
    {
        var server = NewServer(NewName());

        Assert.That(server.Start(_ => { }), Is.False);
        Assert.That(server.IsValid, Is.False);
        Assert.That(server.IsStarted, Is.False);
    }

    [Test]
    public void ServerInitAfterStart_IsRejectedAndTheServerStaysUsable()
    {
        var timers = new ManualTimerFactory();
        using var firstHandler = new RecordingReliableServerHandler(
            MemoryRental.Shared, ReliableServerMode.RespondAndFinish);
        string name = NewName();
        var server = StartServer(name, timers, firstHandler);
        using var lateHandler = new RecordingReliableServerHandler(MemoryRental.Shared);

        Assert.That(server.Init(lateHandler), Is.False);
        Assert.That(server.IsValid, Is.True);

        var client = StartClient(name, new ManualTimerFactory());
        using var clientHandler = new RecordingReliableClientHandler();
        Assert.That(client.Request(TestMessages.Int(1), clientHandler), Is.EqualTo(SendResult.Ok));
        Assert.That(clientHandler.Finished.Wait(Timeout), Is.True);
        Assert.That(firstHandler.RequestValues, Is.EqualTo(new[] { 1 }));
    }

    [Test]
    public void DuplicateServerId_SecondStartFailsAndFirstServerStaysUsable()
    {
        var timers = new ManualTimerFactory();
        using var firstHandler = new RecordingReliableServerHandler(
            MemoryRental.Shared, ReliableServerMode.RespondAndFinish);
        string name = NewName();
        var first = StartServer(name, timers, firstHandler);

        using var secondHandler = new RecordingReliableServerHandler(MemoryRental.Shared);
        var second = NewServer(name);
        _transports.Add(second);
        Assert.That(second.Init(secondHandler), Is.True);
        Assert.That(second.Start(_ => { }), Is.False);
        Assert.That(second.IsValid, Is.False);

        var client = StartClient(name, new ManualTimerFactory());
        using var clientHandler = new RecordingReliableClientHandler();
        Assert.That(client.Request(TestMessages.Int(2), clientHandler), Is.EqualTo(SendResult.Ok));
        Assert.That(clientHandler.Finished.Wait(Timeout), Is.True);
        Assert.That(first.CurrentCarrier!.RegisteredClientCount, Is.EqualTo(1));
    }

    [Test]
    public void NormalStop_SchedulesOneFailedCompletionAndLeavesTheTransportValid()
    {
        var timers = new ManualTimerFactory();
        using var serverHandler = new RecordingReliableServerHandler(MemoryRental.Shared);
        StartServer(NewName(), timers, serverHandler, out string name);
        var client = NewClient(name);
        using var clientHandler = new RecordingReliableClientHandler();

        var reasons = new List<StopReason>();
        Assert.That(client.Start(reason =>
        {
            lock (reasons)
            {
                reasons.Add(reason);
            }
        }), Is.True);
        _transports.Add(client);

        Assert.That(client.Request(TestMessages.Int(3), clientHandler), Is.EqualTo(SendResult.Ok));
        Assert.That(serverHandler.Requested.Wait(Timeout), Is.True);

        Assert.That(client.Stop(), Is.True);
        Assert.That(clientHandler.Finished.Wait(Timeout), Is.True);

        lock (reasons)
        {
            Assert.That(reasons, Has.Count.EqualTo(1));
        }

        Assert.Multiple(() =>
        {
            Assert.That(client.IsValid, Is.True);
            Assert.That(client.IsStarted, Is.False);
            Assert.That(clientHandler.FinishCount, Is.EqualTo(1));
            Assert.That(clientHandler.Finishes, Is.EqualTo(new[] { RRReliableCompletionType.Failed }));
        });
    }

    [Test]
    public void NormalStopThenChannelDisposal_LeavesIsValidTrueAndDoesNotFailAgain()
    {
        var timers = new ManualTimerFactory();
        using var serverHandler = new RecordingReliableServerHandler(MemoryRental.Shared);
        StartServer(NewName(), timers, serverHandler, out string name);
        var client = NewClient(name);
        using var clientHandler = new RecordingReliableClientHandler();

        int stopCount = 0;
        Assert.That(client.Start(_ => Interlocked.Increment(ref stopCount)), Is.True);
        _transports.Add(client);

        Assert.That(client.Request(TestMessages.Int(4), clientHandler), Is.EqualTo(SendResult.Ok));
        Assert.That(serverHandler.Requested.Wait(Timeout), Is.True);
        Channel channel = client.CurrentCarrier!.CurrentChannel!;

        Assert.That(client.Stop(), Is.True);
        Assert.That(clientHandler.Finished.Wait(Timeout), Is.True);
        channel.Dispose();

        Assert.Multiple(() =>
        {
            Assert.That(client.IsValid, Is.True);
            Assert.That(client.IsStarted, Is.False);
            Assert.That(Volatile.Read(ref stopCount), Is.EqualTo(1));
            Assert.That(clientHandler.Finishes, Is.EqualTo(new[] { RRReliableCompletionType.Failed }));
        });
    }

    [Test]
    public void ChannelDisposalAfterALiveSession_FailsOnceAndFinishesPendingRequests()
    {
        var timers = new ManualTimerFactory();
        using var serverHandler = new RecordingReliableServerHandler(MemoryRental.Shared);
        var server = StartServer(NewName(), timers, serverHandler, out string name);
        var client = NewClient(name);
        using var clientHandler = new RecordingReliableClientHandler();

        var stopped = new ManualResetEventSlim(false);
        StopReason? stopReason = null;
        Assert.That(client.Start(reason =>
        {
            stopReason = reason;
            stopped.Set();
        }), Is.True);
        _transports.Add(client);

        Assert.That(client.Request(TestMessages.Int(5), clientHandler), Is.EqualTo(SendResult.Ok));
        Assert.That(serverHandler.Requested.Wait(Timeout), Is.True);

        client.CurrentCarrier!.CurrentChannel!.Dispose();

        Assert.That(stopped.Wait(Timeout), Is.True, "the transport must report the channel loss");
        Assert.That(clientHandler.Finished.Wait(Timeout), Is.True);

        IRRReliableResponse retained = serverHandler.Handles[0];
        Assert.That(retained.Response(TestMessages.Int(50)), Is.EqualTo(SendResult.Error),
            "the server session must be closed before the retained handle can answer");

        Assert.Multiple(() =>
        {
            Assert.That(stopReason, Is.TypeOf<TextFail>());
            Assert.That(((TextFail)stopReason!).Text, Is.EqualTo("direct channel disposed"));
            Assert.That(client.IsValid, Is.False);
            Assert.That(client.IsStarted, Is.False);
            Assert.That(clientHandler.FinishCount, Is.EqualTo(1));
            Assert.That(clientHandler.Finishes, Is.EqualTo(new[] { RRReliableCompletionType.Failed }));
            Assert.That(server.CurrentCarrier!.RegisteredClientCount, Is.Zero);
        });
    }

    [Test]
    public void ChannelDisposalBeforeAnySuccessfulSend_IsNotAFailureAndALaterServerServes()
    {
        string name = NewName();
        var clientTimers = new ManualTimerFactory();
        var client = NewClient(name, clientTimers);
        using var firstHandler = new RecordingReliableClientHandler();
        using var secondHandler = new RecordingReliableClientHandler();

        int stopCount = 0;
        Assert.That(client.Start(_ => Interlocked.Increment(ref stopCount)), Is.True);
        _transports.Add(client);

        var serverEndpoint = new StringEndPoint(name);
        Assert.That(ChannelManager.Instance.RegisterServer(RRDirectInfo.ReliableTransportName, serverEndpoint, channel => channel.Dispose()),
            Is.True);

        try
        {
            Assert.That(client.Request(TestMessages.Int(6), firstHandler),
                Is.EqualTo(SendResult.Error));

            Assert.Multiple(() =>
            {
                Assert.That(firstHandler.ResponseCount, Is.Zero);
                Assert.That(firstHandler.FinishCount, Is.Zero);
                Assert.That(clientTimers.ScheduledCount, Is.Zero);
                Assert.That(Volatile.Read(ref stopCount), Is.Zero);
                Assert.That(client.IsValid, Is.True);
                Assert.That(client.IsStarted, Is.True);
            });
        }
        finally
        {
            ChannelManager.Instance.UnregisterServer(RRDirectInfo.ReliableTransportName, serverEndpoint);
        }

        using var serverHandler = new RecordingReliableServerHandler(
            MemoryRental.Shared, ReliableServerMode.RespondAndFinish);
        StartServer(name, new ManualTimerFactory(), serverHandler);

        Assert.That(client.Request(TestMessages.Int(7), secondHandler), Is.EqualTo(SendResult.Ok));
        Assert.That(secondHandler.Finished.Wait(Timeout), Is.True);
        Assert.That(serverHandler.RequestValues, Is.EqualTo(new[] { 7 }));
    }

    private static string NewName()
    {
        return "rr-reliable-life-" + Guid.NewGuid().ToString("N");
    }

    private RRReliableDirectServer NewServer(string name)
    {
        return new RRReliableDirectServer(
            name,
            TimeSpan.FromSeconds(3),
            Logger,
            MemoryRental.Shared,
            new ManualTimerFactory());
    }

    private RRReliableDirectServer StartServer(
        string name,
        ManualTimerFactory timers,
        IRRReliableServerHandler handler)
    {
        return StartServer(name, timers, handler, out _);
    }

    private RRReliableDirectServer StartServer(
        string name,
        ManualTimerFactory timers,
        IRRReliableServerHandler handler,
        out string boundName)
    {
        boundName = name;
        var server = new RRReliableDirectServer(
            name,
            TimeSpan.FromSeconds(3),
            Logger,
            MemoryRental.Shared,
            timers);
        Assert.That(server.Init(handler), Is.True);
        Assert.That(server.Start(_ => { }), Is.True);
        _transports.Add(server);
        return server;
    }

    private RRReliableDirectClient NewClient(string name)
    {
        return NewClient(name, new ManualTimerFactory());
    }

    private RRReliableDirectClient NewClient(string name, ManualTimerFactory timers)
    {
        return new RRReliableDirectClient(
            new StringEndPoint(name),
            TimeSpan.FromSeconds(3),
            Logger,
            MemoryRental.Shared,
            timers);
    }

    private RRReliableDirectClient StartClient(string name, ManualTimerFactory timers)
    {
        var client = new RRReliableDirectClient(
            new StringEndPoint(name),
            TimeSpan.FromSeconds(3),
            Logger,
            MemoryRental.Shared,
            timers);
        Assert.That(client.Start(_ => { }), Is.True);
        _transports.Add(client);
        return client;
    }
}
