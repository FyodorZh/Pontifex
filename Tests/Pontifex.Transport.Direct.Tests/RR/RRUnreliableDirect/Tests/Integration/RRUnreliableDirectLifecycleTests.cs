using System;
using System.Threading;
using Actuarius.Memory;
using Pontifex.Endpoints;
using Pontifex.RR.Direct;
using Pontifex.RR.Unreliable;
using Pontifex.RR.Unreliable.Direct;
using Pontifex.Tests.RR.RRUnreliableDirect.Harness;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Tests.RR.RRUnreliableDirect;

/// <summary>
/// Pins the lifecycle, start gating, registry, and reconnect behavior of the in-process RR
/// unreliable carrier.
/// </summary>
[TestFixture]
[Category("Direct.RRUnreliable")]
[Category("Direct.RRUnreliable.Integration")]
public sealed class RRUnreliableDirectLifecycleTests
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
        var client = new RRUnreliableDirectClient(
            new StringEndPoint(NewName()),
            TimeSpan.FromSeconds(3),
            Logger,
            MemoryRental.Shared,
            timeProvider: NewClock());
        _transports.Add(client);

        Assert.That(client.Start(_ => { }), Is.True);
        Assert.That(client.Start(_ => { }), Is.False);
        Assert.That(client.IsStarted, Is.True);
    }

    [Test]
    public void ServerInitAfterStart_IsRejectedAndTheServerStaysUsable()
    {
        var clock = NewClock();
        using var firstHandler = new RecordingServerHandler(MemoryRental.Shared);
        string name = NewName();
        var server = StartServer(name, clock, firstHandler);
        using var lateHandler = new RecordingServerHandler(MemoryRental.Shared);

        Assert.That(server.Init(lateHandler), Is.False);
        Assert.That(server.IsValid, Is.True);

        var client = StartClient(name, clock);
        using var clientHandler = new RecordingClientHandler();
        Assert.That(client.Request(TestMessages.Int(3), clientHandler), Is.EqualTo(SendResult.Ok));
        Assert.That(firstHandler.Requested.Wait(Timeout), Is.True);
        Assert.That(clientHandler.Responded.Wait(Timeout), Is.True);
    }

    [Test]
    public void ServerInit_IsOneTimeAndRejectsNull()
    {
        using var handler = new RecordingServerHandler(MemoryRental.Shared);
        var server = NewServer(NewName(), NewClock());

        Assert.That(() => server.Init(null!), Throws.TypeOf<ArgumentNullException>());
        Assert.That(server.Init(handler), Is.True);
        Assert.That(server.Init(handler), Is.False);
    }

    [Test]
    public void ServerStartWithoutInit_ReturnsFalseAndInvalidates()
    {
        var server = NewServer(NewName(), NewClock());

        Assert.That(server.Start(_ => { }), Is.False);
        Assert.That(server.IsValid, Is.False);
        Assert.That(server.IsStarted, Is.False);
    }

    [Test]
    public void DuplicateServerId_SecondStartFailsAndFirstServerStaysUsable()
    {
        var clock = NewClock();
        using var firstHandler = new RecordingServerHandler(MemoryRental.Shared);
        string name = NewName();
        var first = StartServer(name, clock, firstHandler);

        using var secondHandler = new RecordingServerHandler(MemoryRental.Shared);
        var second = NewServer(name, clock);
        Assert.That(second.Init(secondHandler), Is.True);
        Assert.That(second.Start(_ => { }), Is.False);
        Assert.That(second.IsValid, Is.False);

        var client = StartClient(name, clock);
        using var clientHandler = new RecordingClientHandler();
        Assert.That(client.Request(TestMessages.Int(5), clientHandler), Is.EqualTo(SendResult.Ok));
        Assert.That(firstHandler.Requested.Wait(Timeout), Is.True);
        Assert.That(clientHandler.Responded.Wait(Timeout), Is.True);
        Assert.That(first.CurrentCarrier!.RegisteredClientCount, Is.EqualTo(1));
    }

    [Test]
    public void Stop_ClearsThePendingWindowWithoutCallbacks()
    {
        var clock = NewClock();
        using var serverHandler = new RecordingServerHandler(
            MemoryRental.Shared, ServerResponseMode.AcceptSilently);
        var server = StartServer(NewName(), clock, serverHandler, out string name);
        var client = StartClient(name, clock);
        using var clientHandler = new RecordingClientHandler();

        Assert.That(client.Request(TestMessages.Int(1), clientHandler), Is.EqualTo(SendResult.Ok));
        Assert.That(serverHandler.Requested.Wait(Timeout), Is.True);
        Assert.That(client.Stop(), Is.True);

        Assert.That(clientHandler.ResponseCount, Is.Zero);
        Assert.That(() => client.Request(TestMessages.Int(2), clientHandler),
            Is.EqualTo(SendResult.NotConnected));
    }

    [Test]
    public void NoServer_RequestIsAcceptedAndExpiresThenALaterServerServes()
    {
        var clock = NewClock();
        string name = NewName();
        var client = StartClient(name, clock);
        using var firstHandler = new RecordingClientHandler();
        using var secondHandler = new RecordingClientHandler();

        Assert.That(client.Request(TestMessages.Int(1), firstHandler), Is.EqualTo(SendResult.Ok));
        Assert.That(firstHandler.ResponseCount, Is.Zero);

        using var serverHandler = new RecordingServerHandler(MemoryRental.Shared);
        StartServer(name, clock, serverHandler);

        Assert.That(client.Request(TestMessages.Int(2), secondHandler), Is.EqualTo(SendResult.Ok));
        Assert.That(serverHandler.Requested.Wait(Timeout), Is.True);
        Assert.That(secondHandler.Responded.Wait(Timeout), Is.True);

        Assert.Multiple(() =>
        {
            Assert.That(firstHandler.ResponseCount, Is.Zero);
            Assert.That(secondHandler.ResponseCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void RequestBeforeOnStarted_IsPresentedExactlyOnceAfterTheGateOpens()
    {
        var clock = NewClock();
        using var serverHandler = new RecordingServerHandler(MemoryRental.Shared);
        int startReturned = 0;
        serverHandler.StartReturnedProbe = () => Volatile.Read(ref startReturned) != 0;

        string name = NewName();
        var server = NewServer(name, clock);
        Assert.That(server.Init(serverHandler), Is.True);

        var gateReached = new ManualResetEventSlim(false);
        var gateRelease = new ManualResetEventSlim(false);
        server.CurrentCarrier!.StartGateForTests = () =>
        {
            gateReached.Set();
            gateRelease.Wait(Timeout);
        };

        bool startResult = false;
        var startThread = new Thread(() =>
        {
            startResult = server.Start(_ => { });
            Interlocked.Exchange(ref startReturned, 1);
        });
        startThread.Start();
        Assert.That(gateReached.Wait(Timeout), Is.True, "the start gate was not reached");

        _transports.Add(server);
        var client = StartClient(name, clock);
        using var clientHandler = new RecordingClientHandler();
        using var concurrentHandler = new RecordingClientHandler();

        Assert.That(client.Request(TestMessages.Int(7), clientHandler), Is.EqualTo(SendResult.Ok));
        Assert.That(serverHandler.RequestCount, Is.Zero, "no callback may begin before OnStarted");

        var sendGo = new ManualResetEventSlim(false);
        SendResult concurrentResult = SendResult.Unknown;
        var sendThread = new Thread(() =>
        {
            sendGo.Wait();
            concurrentResult = client.Request(TestMessages.Int(8), concurrentHandler);
        });
        sendThread.Start();
        sendGo.Set();

        gateRelease.Set();
        Assert.That(startThread.Join(Timeout), Is.True);
        Assert.That(sendThread.Join(Timeout), Is.True);
        Assert.That(startResult, Is.True);
        Assert.That(concurrentResult, Is.EqualTo(SendResult.Ok));

        Assert.That(serverHandler.Requested.Wait(Timeout), Is.True);
        Assert.That(clientHandler.Responded.Wait(Timeout), Is.True);
        Assert.That(concurrentHandler.Responded.Wait(Timeout), Is.True);

        Assert.Multiple(() =>
        {
            Assert.That(serverHandler.RequestCount, Is.EqualTo(2));
            Assert.That(clientHandler.ResponseCount, Is.EqualTo(1));
            Assert.That(concurrentHandler.ResponseCount, Is.EqualTo(1));
            Assert.That(serverHandler.RequestValues, Is.EqualTo(new[] { 7, 8 }));
            Assert.That(serverHandler.ObservedStartReturned, Is.Not.Null,
                "the handler records whether it observed Start returning; the deviation is accepted");
        });
    }

    [Test]
    public void PreStartPendingList_ReleasesThe101stFrame()
    {
        var clock = NewClock();
        using var serverHandler = new RecordingServerHandler(MemoryRental.Shared);
        string name = NewName();
        var server = NewServer(name, clock);
        Assert.That(server.Init(serverHandler), Is.True);

        var gateReached = new ManualResetEventSlim(false);
        var gateRelease = new ManualResetEventSlim(false);
        server.CurrentCarrier!.StartGateForTests = () =>
        {
            gateReached.Set();
            gateRelease.Wait(Timeout);
        };

        bool startResult = false;
        var startThread = new Thread(() => startResult = server.Start(_ => { }));
        startThread.Start();
        Assert.That(gateReached.Wait(Timeout), Is.True);

        _transports.Add(server);
        var client = StartClient(name, clock);
        var handlers = new RecordingClientHandler[RRDirectInfo.DispatchQueueCapacity + 1];
        var frames = new UnionDataList[handlers.Length];

        for (int i = 0; i < handlers.Length; i++)
        {
            handlers[i] = new RecordingClientHandler();
            frames[i] = TestMessages.Int(i);
            Assert.That(client.Request(frames[i], handlers[i]), Is.EqualTo(SendResult.Ok));
        }

        Assert.That(frames[^1].IsAlive, Is.False,
            "the 101st pre-start frame must be released without blocking");

        gateRelease.Set();
        Assert.That(startThread.Join(Timeout), Is.True);
        Assert.That(startResult, Is.True);
        WaitUntil(() => serverHandler.RequestCount == handlers.Length - 1, Timeout);

        foreach (RecordingClientHandler handler in handlers)
        {
            handler.Dispose();
        }
    }

    [Test]
    public void RejectingTheLiveChannel_PurgesTheRegistryAndAllowsReconnect()
    {
        var clock = NewClock();
        using var serverHandler = new RecordingServerHandler(MemoryRental.Shared);
        var server = StartServer(NewName(), clock, serverHandler, out string name);
        var client = StartClient(name, clock);
        using var firstHandler = new RecordingClientHandler();
        using var secondHandler = new RecordingClientHandler();

        Assert.That(client.Request(TestMessages.Int(1), firstHandler), Is.EqualTo(SendResult.Ok));
        Assert.That(firstHandler.Responded.Wait(Timeout), Is.True);

        Channel channel = client.CurrentChannel!;
        server.CurrentCarrier!.RejectChannelForTests(channel.ClientEp, channel);

        Assert.That(server.CurrentCarrier.RegisteredClientCount, Is.Zero);
        Assert.That(client.CurrentChannel, Is.Null);

        Assert.That(client.Request(TestMessages.Int(2), secondHandler), Is.EqualTo(SendResult.Ok));
        Assert.That(secondHandler.Responded.Wait(Timeout), Is.True);
        Assert.That(serverHandler.RequestCount, Is.EqualTo(2));

        Assert.That(server.Stop(), Is.True);
        var restarted = StartServer(name, clock, serverHandler);
        using var thirdHandler = new RecordingClientHandler();
        Assert.That(client.Request(TestMessages.Int(3), thirdHandler), Is.EqualTo(SendResult.Ok));
        Assert.That(thirdHandler.Responded.Wait(Timeout), Is.True);
        Assert.That(restarted.CurrentCarrier!.RegisteredClientCount, Is.EqualTo(1));
    }

    [Test]
    public void DisposingTheLiveChannel_IsALossAndTheNextRequestReconnects()
    {
        var clock = NewClock();
        using var serverHandler = new RecordingServerHandler(MemoryRental.Shared);
        var server = StartServer(NewName(), clock, serverHandler, out string name);
        var client = StartClient(name, clock);
        using var firstHandler = new RecordingClientHandler();
        using var secondHandler = new RecordingClientHandler();

        Assert.That(client.Request(TestMessages.Int(1), firstHandler), Is.EqualTo(SendResult.Ok));
        Assert.That(firstHandler.Responded.Wait(Timeout), Is.True);

        client.CurrentChannel!.Dispose();
        Assert.That(client.CurrentChannel, Is.Null);
        Assert.That(server.CurrentCarrier!.RegisteredClientCount, Is.Zero);

        Assert.That(client.Request(TestMessages.Int(2), secondHandler), Is.EqualTo(SendResult.Ok));
        Assert.That(secondHandler.Responded.Wait(Timeout), Is.True);
        Assert.That(serverHandler.RequestCount, Is.EqualTo(2));
    }

    [Test]
    public void ServerHandler_IsNeverConcurrent()
    {
        var clock = NewClock();
        var serverHandler = new RecordingServerHandler(MemoryRental.Shared);
        serverHandler.Entered = new ManualResetEventSlim(false);
        serverHandler.Release = new ManualResetEventSlim(false);
        using var handlerScope = serverHandler;
        StartServer(NewName(), clock, serverHandler, out string name);

        var firstClient = StartClient(name, clock);
        var secondClient = StartClient(name, clock);
        using var firstHandler = new RecordingClientHandler();
        using var secondHandler = new RecordingClientHandler();

        Assert.That(firstClient.Request(TestMessages.Int(1), firstHandler),
            Is.EqualTo(SendResult.Ok));
        Assert.That(secondClient.Request(TestMessages.Int(2), secondHandler),
            Is.EqualTo(SendResult.Ok));

        serverHandler.Release!.Set();
        Assert.That(firstHandler.Responded.Wait(Timeout), Is.True);
        Assert.That(secondHandler.Responded.Wait(Timeout), Is.True);

        Assert.That(serverHandler.MaxActiveCount, Is.EqualTo(1));
    }

    private static ManualClock NewClock()
    {
        return new ManualClock { Now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) };
    }

    private static string NewName()
    {
        return "rr-lifecycle-" + Guid.NewGuid().ToString("N");
    }

    private static void WaitUntil(Func<bool> condition, TimeSpan timeout)
    {
        DateTime deadline = DateTime.UtcNow + timeout;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                Assert.Fail("the condition was not met before the timeout");
            }

            Thread.Yield();
        }
    }

    private RRUnreliableDirectServer NewServer(string name, ManualClock clock)
    {
        var server = new RRUnreliableDirectServer(
            name,
            TimeSpan.FromSeconds(3),
            Logger,
            MemoryRental.Shared,
            clock);
        return server;
    }

    private RRUnreliableDirectServer StartServer(
        string name,
        ManualClock clock,
        IRRUnreliableServerHandler handler)
    {
        var server = NewServer(name, clock);
        Assert.That(server.Init(handler), Is.True);
        Assert.That(server.Start(_ => { }), Is.True);
        _transports.Add(server);
        return server;
    }

    private RRUnreliableDirectServer StartServer(
        string name,
        ManualClock clock,
        IRRUnreliableServerHandler handler,
        out string serverName)
    {
        serverName = name;
        return StartServer(name, clock, handler);
    }

    private RRUnreliableDirectClient StartClient(string name, ManualClock clock)
    {
        var client = new RRUnreliableDirectClient(
            new StringEndPoint(name),
            TimeSpan.FromSeconds(3),
            Logger,
            MemoryRental.Shared,
            timeProvider: clock);
        Assert.That(client.Start(_ => { }), Is.True);
        _transports.Add(client);
        return client;
    }
}
