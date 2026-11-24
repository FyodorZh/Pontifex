using System;
using System.Collections.Generic;
using System.Threading;
using Actuarius.Memory;
using Pontifex.Endpoints;
using Pontifex.RR.Direct;
using Pontifex.RR.Unreliable;
using Pontifex.RR.Unreliable.Direct;
using Pontifex.Tests.RR.RRUnreliableDirect.Harness;
using Pontifex.Utils;
using Pontifex.VirtualDelivery;
using Scriba;

namespace Pontifex.Tests.RR.RRUnreliableDirect;

/// <summary>
/// Pins the shared Direct RR carrier: delivery-system ownership, scripted delivery, capacity, and the
/// accept-without-peer and live-session rules.
/// </summary>
[TestFixture]
[Category("Direct.RRUnreliable")]
public sealed class RRUnreliableDirectCarrierTests
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
    public void TrackingFactories_CreateFreshSystemsAfterAReconnect()
    {
        var clock = NewClock();
        using var serverHandler = new RecordingServerHandler(MemoryRental.Shared);
        StartServer(clock, serverHandler, out string name);

        var created = new List<TrackingDeliverySystem>();
        Func<IDeliverySystem> factory = () =>
        {
            var system = new TrackingDeliverySystem();
            lock (created)
            {
                created.Add(system);
            }

            return system;
        };

        var client = new RRUnreliableDirectClient(
            new StringEndPoint(name),
            TimeSpan.FromSeconds(3),
            Logger,
            MemoryRental.Shared,
            factory,
            factory,
            clock);
        Assert.That(client.Start(_ => { }), Is.True);
        _transports.Add(client);
        using var firstHandler = new RecordingClientHandler();
        using var secondHandler = new RecordingClientHandler();

        Assert.That(client.Request(TestMessages.Int(1), firstHandler), Is.EqualTo(SendResult.Ok));
        Assert.That(firstHandler.Responded.Wait(Timeout), Is.True);

        TrackingDeliverySystem[] firstConnect;
        lock (created)
        {
            firstConnect = created.ToArray();
        }

        Assert.That(firstConnect, Has.Length.EqualTo(2));
        Assert.That(firstConnect[0], Is.Not.SameAs(firstConnect[1]));

        client.CurrentChannel!.Dispose();
        Assert.That(firstConnect[0].IsReleased, Is.True);
        Assert.That(firstConnect[1].IsReleased, Is.True);

        Assert.That(client.Request(TestMessages.Int(2), secondHandler), Is.EqualTo(SendResult.Ok));
        Assert.That(secondHandler.Responded.Wait(Timeout), Is.True);

        TrackingDeliverySystem[] bothConnects;
        lock (created)
        {
            bothConnects = created.ToArray();
        }

        Assert.That(bothConnects, Has.Length.EqualTo(4));
        Assert.That(bothConnects[2], Is.Not.SameAs(bothConnects[0]));
        Assert.That(bothConnects[3], Is.Not.SameAs(bothConnects[1]));
    }

    [Test]
    public void DuplicateResponse_YieldsExactlyOneCallback()
    {
        var clock = NewClock();
        using var serverHandler = new RecordingServerHandler(MemoryRental.Shared);
        StartServer(clock, serverHandler, out string name);

        var scripted = new ScriptedDeliverySystem()
            .WithDefault(DeliveryAction.Duplicate);

        var client = new RRUnreliableDirectClient(
            new StringEndPoint(name),
            TimeSpan.FromSeconds(3),
            Logger,
            MemoryRental.Shared,
            serverToClientFactory: () => scripted,
            timeProvider: clock);
        Assert.That(client.Start(_ => { }), Is.True);
        _transports.Add(client);
        using var clientHandler = new RecordingClientHandler();

        Assert.That(client.Request(TestMessages.Int(9), clientHandler), Is.EqualTo(SendResult.Ok));
        Assert.That(clientHandler.Responded.Wait(Timeout), Is.True);

        Assert.Multiple(() =>
        {
            Assert.That(scripted.DuplicateCount, Is.EqualTo(1));
            Assert.That(clientHandler.ResponseCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void DroppedRequest_ExpiresSilently()
    {
        var clock = NewClock();
        using var serverHandler = new RecordingServerHandler(MemoryRental.Shared);
        StartServer(clock, serverHandler, out string name);

        var scripted = new ScriptedDeliverySystem()
            .WithDefault(DeliveryAction.Drop);

        var client = new RRUnreliableDirectClient(
            new StringEndPoint(name),
            TimeSpan.FromSeconds(3),
            Logger,
            MemoryRental.Shared,
            clientToServerFactory: () => scripted,
            timeProvider: clock);
        Assert.That(client.Start(_ => { }), Is.True);
        _transports.Add(client);
        using var firstHandler = new RecordingClientHandler();
        using var secondHandler = new RecordingClientHandler();

        Assert.That(client.Request(TestMessages.Int(1), firstHandler), Is.EqualTo(SendResult.Ok));
        Assert.That(serverHandler.RequestCount, Is.Zero);

        clock.Now += TimeSpan.FromSeconds(4);
        Assert.That(client.Request(TestMessages.Int(2), secondHandler), Is.EqualTo(SendResult.Ok));

        Assert.Multiple(() =>
        {
            Assert.That(scripted.DropCount, Is.EqualTo(2));
            Assert.That(serverHandler.RequestCount, Is.Zero);
            Assert.That(firstHandler.ResponseCount, Is.Zero);
            Assert.That(secondHandler.ResponseCount, Is.Zero);
        });
    }

    [Test]
    public void ThrowingDeliverySystem_ReturnsErrorAndReleasesTheFrame()
    {
        var clock = NewClock();
        using var serverHandler = new RecordingServerHandler(MemoryRental.Shared);
        StartServer(clock, serverHandler, out string name);

        var client = new RRUnreliableDirectClient(
            new StringEndPoint(name),
            TimeSpan.FromSeconds(3),
            Logger,
            MemoryRental.Shared,
            clientToServerFactory: () => new TrackingDeliverySystem(throwOnDeliver: true),
            timeProvider: clock);
        Assert.That(client.Start(_ => { }), Is.True);
        _transports.Add(client);
        using var clientHandler = new RecordingClientHandler();
        var request = TestMessages.Int(1);

        Assert.That(client.Request(request, clientHandler), Is.EqualTo(SendResult.Error));
        Assert.That(request.IsAlive, Is.False);
        Assert.That(clientHandler.ResponseCount, Is.Zero);
    }

    [Test]
    public void FullDispatchQueue_ReleasesTheFrameWithoutBlocking()
    {
        var clock = NewClock();
        var serverHandler = new RecordingServerHandler(MemoryRental.Shared);
        serverHandler.Entered = new ManualResetEventSlim(false);
        serverHandler.Release = new ManualResetEventSlim(false);
        using var handlerScope = serverHandler;

        StartServer(clock, serverHandler, out string name, queueCapacity: 2);
        var client = StartClient(name, clock);
        var handlers = new RecordingClientHandler[4];
        var frames = new UnionDataList[4];

        for (int i = 0; i < handlers.Length; i++)
        {
            handlers[i] = new RecordingClientHandler();
            frames[i] = TestMessages.Int(i);
        }

        Assert.That(client.Request(frames[0], handlers[0]), Is.EqualTo(SendResult.Ok));
        Assert.That(serverHandler.Entered!.Wait(Timeout), Is.True);

        Assert.That(client.Request(frames[1], handlers[1]), Is.EqualTo(SendResult.Ok));
        Assert.That(client.Request(frames[2], handlers[2]), Is.EqualTo(SendResult.Ok));
        Assert.That(client.Request(frames[3], handlers[3]), Is.EqualTo(SendResult.Ok));

        Assert.That(frames[3].IsAlive, Is.False,
            "a frame posted to a full dispatch queue must be released without blocking");

        serverHandler.Release!.Set();
        WaitUntil(() => serverHandler.RequestCount == 3, Timeout);

        foreach (RecordingClientHandler handler in handlers)
        {
            handler.Dispose();
        }
    }

    [Test]
    public void FramesQueuedAtStop_AreReleased()
    {
        var clock = NewClock();
        var serverHandler = new RecordingServerHandler(MemoryRental.Shared);
        serverHandler.Entered = new ManualResetEventSlim(false);
        serverHandler.Release = new ManualResetEventSlim(false);
        using var handlerScope = serverHandler;

        var server = StartServer(clock, serverHandler, out string name, queueCapacity: 2);
        var client = StartClient(name, clock);
        var handlers = new RecordingClientHandler[3];
        var frames = new UnionDataList[3];

        for (int i = 0; i < handlers.Length; i++)
        {
            handlers[i] = new RecordingClientHandler();
            frames[i] = TestMessages.Int(i);
        }

        Assert.That(client.Request(frames[0], handlers[0]), Is.EqualTo(SendResult.Ok));
        Assert.That(serverHandler.Entered!.Wait(Timeout), Is.True);
        Assert.That(client.Request(frames[1], handlers[1]), Is.EqualTo(SendResult.Ok));
        Assert.That(client.Request(frames[2], handlers[2]), Is.EqualTo(SendResult.Ok));

        Assert.That(server.Stop(), Is.True);

        Assert.Multiple(() =>
        {
            Assert.That(frames[1].IsAlive, Is.False);
            Assert.That(frames[2].IsAlive, Is.False);
        });

        serverHandler.Release!.Set();

        foreach (RecordingClientHandler handler in handlers)
        {
            handler.Dispose();
        }
    }

    [Test]
    public void FrameDeliveredAfterStop_IsReleasedExactlyOnce()
    {
        var clock = NewClock();
        using var serverHandler = new RecordingServerHandler(MemoryRental.Shared);
        RRUnreliableDirectServer server = StartServer(clock, serverHandler, out string name);

        var serverEndpoint = new StringEndPoint(name);
        var clientEndPoint = new GuidEndPoint(Guid.NewGuid());
        Channel? channel = ChannelManager.Instance.Connect(RRDirectInfo.UnreliableTransportName, serverEndpoint, clientEndPoint);
        Assert.That(channel, Is.Not.Null);
        Action<IEndPoint, UnionDataList> handler = CaptureServerHandler(channel!);

        Assert.That(server.Stop(), Is.True);

        var lateFrame = TestMessages.Int(99);
        handler(clientEndPoint, lateFrame);

        Assert.Multiple(() =>
        {
            Assert.That(lateFrame.IsAlive, Is.False,
                "a frame delivered after the stop snapshot must still be released");
            Assert.That(lateFrame.RefCount.RefCount, Is.Zero);
            Assert.That(serverHandler.RequestCount, Is.Zero);
        });
    }

    [Test]
    public void PendingFrameAtStop_IsReleasedAndStartReportsFalse()
    {
        var clock = NewClock();
        using var serverHandler = new RecordingServerHandler(MemoryRental.Shared);
        string name = NewName();
        var server = new RRUnreliableDirectServer(
            name,
            TimeSpan.FromSeconds(3),
            Logger,
            MemoryRental.Shared,
            clock);
        Assert.That(server.Init(serverHandler), Is.True);

        var gateReached = new ManualResetEventSlim(false);
        var stopReleased = new ManualResetEventSlim(false);
        server.CurrentCarrier!.StartGateForTests = () =>
        {
            gateReached.Set();
            stopReleased.Wait(Timeout);
            server.Stop();
        };

        bool startResult = true;
        var startThread = new Thread(() => startResult = server.Start(_ => { }));
        startThread.Start();
        Assert.That(gateReached.Wait(Timeout), Is.True);

        var client = StartClient(name, clock);
        using var clientHandler = new RecordingClientHandler();
        var request = TestMessages.Int(1);
        Assert.That(client.Request(request, clientHandler), Is.EqualTo(SendResult.Ok));
        Assert.That(serverHandler.RequestCount, Is.Zero);

        stopReleased.Set();
        Assert.That(startThread.Join(Timeout), Is.True);

        Assert.Multiple(() =>
        {
            Assert.That(startResult, Is.False);
            Assert.That(request.IsAlive, Is.False);
            Assert.That(serverHandler.RequestCount, Is.Zero);
        });
    }

    [Test]
    public void CarrierWithoutPeer_ReturnsTheNoPeerPolicyAndReportsLiveSessionOnce()
    {
        var serverEndpoint = new StringEndPoint(NewName());
        int disposedCount = 0;
        int liveCount = 0;
        var carrier = new DirectRrChannelClient(
            RRDirectInfo.UnreliableTransportName,
            serverEndpoint,
            new GuidEndPoint(Guid.NewGuid()),
            Logger,
            _ => { },
            () => Interlocked.Increment(ref disposedCount),
            () => Interlocked.Increment(ref liveCount));

        var noPeerFrame = TestMessages.Int(1);
        Assert.That(carrier.TrySend(noPeerFrame, acceptWithoutPeer: false),
            Is.EqualTo(SendResult.Error));
        Assert.That(noPeerFrame.IsAlive, Is.False);

        var server = new DirectRrChannelServer(RRDirectInfo.UnreliableTransportName, serverEndpoint.EP, Logger, (_, frame) => frame.Release());
        Assert.That(server.TryStart(), Is.True);
        try
        {
            Assert.That(carrier.TrySend(TestMessages.Int(2), acceptWithoutPeer: false),
                Is.EqualTo(SendResult.Ok));
            Assert.That(carrier.TrySend(TestMessages.Int(3), acceptWithoutPeer: false),
                Is.EqualTo(SendResult.Ok));

            Assert.Multiple(() =>
            {
                Assert.That(liveCount, Is.EqualTo(1));
                Assert.That(disposedCount, Is.Zero);
            });

            carrier.CurrentChannel!.Dispose();
            Assert.That(disposedCount, Is.EqualTo(1));
            Assert.That(carrier.CurrentChannel, Is.Null);
        }
        finally
        {
            server.Stop();
            carrier.Stop();
        }
    }

    [Test]
    public void SystemInstalledOnARejectedChannel_IsReleased()
    {
        var serverEndpoint = new StringEndPoint(NewName());
        Assert.That(ChannelManager.Instance.RegisterServer(RRDirectInfo.UnreliableTransportName, serverEndpoint, channel => channel.Dispose()),
            Is.True);

        var clientToServer = new TrackingDeliverySystem();
        var serverToClient = new TrackingDeliverySystem();
        var carrier = new DirectRrChannelClient(
            RRDirectInfo.UnreliableTransportName,
            serverEndpoint,
            new GuidEndPoint(Guid.NewGuid()),
            Logger,
            _ => { },
            clientToServerFactory: () => clientToServer,
            serverToClientFactory: () => serverToClient);

        try
        {
            Assert.That(carrier.TrySend(TestMessages.Int(1), acceptWithoutPeer: true),
                Is.EqualTo(SendResult.Ok));

            Assert.Multiple(() =>
            {
                Assert.That(clientToServer.IsReleased, Is.True);
                Assert.That(serverToClient.IsReleased, Is.True);
                Assert.That(carrier.CurrentChannel, Is.Null);
            });
        }
        finally
        {
            carrier.Stop();
            ChannelManager.Instance.UnregisterServer(RRDirectInfo.UnreliableTransportName, serverEndpoint);
        }
    }

    [Test]
    public void ChannelDisposedFromADeliveryFactory_ReleasesEveryCreatedSystemAndTheFrame()
    {
        var serverEndpoint = new StringEndPoint(NewName());
        Channel? channel = null;
        Assert.That(ChannelManager.Instance.RegisterServer(RRDirectInfo.UnreliableTransportName, serverEndpoint, created => channel = created),
            Is.True);

        var clientToServer = new TrackingDeliverySystem();
        var serverToClient = new TrackingDeliverySystem();
        var carrier = new DirectRrChannelClient(
            RRDirectInfo.UnreliableTransportName,
            serverEndpoint,
            new GuidEndPoint(Guid.NewGuid()),
            Logger,
            _ => { },
            clientToServerFactory: () =>
            {
                // The channel is disposed before the created system can be installed. A disposed channel
                // already exchanged and released both installed systems, so it cannot release a system
                // installed afterwards.
                channel!.Dispose();
                return clientToServer;
            },
            serverToClientFactory: () => serverToClient);

        try
        {
            var frame = TestMessages.Int(1);
            Assert.That(carrier.TrySend(frame, acceptWithoutPeer: true), Is.EqualTo(SendResult.Ok));

            Assert.Multiple(() =>
            {
                Assert.That(clientToServer.ClearCount, Is.EqualTo(1));
                Assert.That(clientToServer.DisposeCount, Is.EqualTo(1));
                Assert.That(serverToClient.ClearCount, Is.EqualTo(1));
                Assert.That(serverToClient.DisposeCount, Is.EqualTo(1));
                Assert.That(frame.IsAlive, Is.False);
                Assert.That(frame.RefCount.RefCount, Is.Zero);
                Assert.That(carrier.CurrentChannel, Is.Null);
            });
        }
        finally
        {
            carrier.Stop();
            ChannelManager.Instance.UnregisterServer(RRDirectInfo.UnreliableTransportName, serverEndpoint);
        }
    }

    [Test]
    public void TrySendToARemovedDestination_ReturnsErrorAndReleasesTheFrame()
    {
        var serverEndpoint = new StringEndPoint(NewName());
        var clientEndpoint = new GuidEndPoint(Guid.NewGuid());
        var server = new DirectRrChannelServer(
            RRDirectInfo.UnreliableTransportName,
            serverEndpoint.EP, Logger, (_, frame) => frame.Release());
        Assert.That(server.TryStart(), Is.True);

        try
        {
            Channel channel = ChannelManager.Instance.Connect(RRDirectInfo.UnreliableTransportName, serverEndpoint, clientEndpoint)!;
            Assert.That(channel, Is.Not.Null);
            Assert.That(server.RegisteredClientCount, Is.EqualTo(1));
            Assert.That(server.TrySend(clientEndpoint, TestMessages.Int(1)),
                Is.EqualTo(SendResult.Ok));

            Assert.That(ChannelManager.Instance.Disconnect(RRDirectInfo.UnreliableTransportName, serverEndpoint, clientEndpoint),
                Is.True);
            Assert.That(server.RegisteredClientCount, Is.Zero);

            var frame = TestMessages.Int(2);
            Assert.That(server.TrySend(clientEndpoint, frame), Is.EqualTo(SendResult.Error));
            Assert.That(frame.IsAlive, Is.False);
        }
        finally
        {
            server.Stop();
        }
    }

    [Test]
    public void ServerStop_ClearsTheRegisteredClientCount()
    {
        var serverEndpoint = new StringEndPoint(NewName());
        var server = new DirectRrChannelServer(
            RRDirectInfo.UnreliableTransportName,
            serverEndpoint.EP, Logger, (_, frame) => frame.Release());
        Assert.That(server.TryStart(), Is.True);

        Channel channel = ChannelManager.Instance.Connect(RRDirectInfo.UnreliableTransportName, 
            serverEndpoint, new GuidEndPoint(Guid.NewGuid()))!;
        Assert.That(channel, Is.Not.Null);
        Assert.That(server.RegisteredClientCount, Is.EqualTo(1));

        server.Stop();

        Assert.Multiple(() =>
        {
            Assert.That(server.RegisteredClientCount, Is.Zero);
            Assert.That(channel.IsDisposed, Is.True);
            Assert.That(
                ChannelManager.Instance.Connect(RRDirectInfo.UnreliableTransportName, serverEndpoint, new GuidEndPoint(Guid.NewGuid())),
                Is.Null);
        });
    }

    [Test]
    public void ResponseTtl_StartsAtReceiptBeforeTheDispatchQueueRuns()
    {
        var clock = NewClock();
        var serverHandler = new RecordingServerHandler(MemoryRental.Shared);
        serverHandler.Entered = new ManualResetEventSlim(false);
        serverHandler.Release = new ManualResetEventSlim(false);
        using var handlerScope = serverHandler;

        StartServer(clock, serverHandler, out string name, queueCapacity: 2);
        var client = StartClient(name, clock);
        using var firstHandler = new RecordingClientHandler();
        using var secondHandler = new RecordingClientHandler();

        Assert.That(client.Request(TestMessages.Int(1), firstHandler), Is.EqualTo(SendResult.Ok));
        Assert.That(serverHandler.Entered!.Wait(Timeout), Is.True);

        Assert.That(client.Request(TestMessages.Int(2), secondHandler), Is.EqualTo(SendResult.Ok));

        // Both request handles were created when the frames arrived. The second request is still
        // queued; advancing the clock now must close both handles before either can answer.
        clock.Now += TimeSpan.FromSeconds(4);
        serverHandler.Release!.Set();

        WaitUntil(() => serverHandler.RequestCount == 2, Timeout);

        Assert.Multiple(() =>
        {
            Assert.That(serverHandler.ResponseResults, Is.All.EqualTo(SendResult.Error));
            Assert.That(firstHandler.ResponseCount, Is.Zero);
            Assert.That(secondHandler.ResponseCount, Is.Zero);
        });
    }

    [Test]
    public void SendAfterChannelLoss_ReturnsTheNoPeerPolicyNotNotConnected()
    {
        var serverEndpoint = new StringEndPoint(NewName());
        var server = new DirectRrChannelServer(
            RRDirectInfo.UnreliableTransportName,
            serverEndpoint.EP, Logger, (_, frame) => frame.Release());
        Assert.That(server.TryStart(), Is.True);

        var carrier = new DirectRrChannelClient(
            RRDirectInfo.UnreliableTransportName,
            serverEndpoint,
            new GuidEndPoint(Guid.NewGuid()),
            Logger,
            _ => { });

        try
        {
            Assert.That(carrier.TrySend(TestMessages.Int(1), acceptWithoutPeer: true),
                Is.EqualTo(SendResult.Ok));
            Assert.That(carrier.CurrentChannel, Is.Not.Null);

            server.Stop();
            Assert.That(carrier.CurrentChannel, Is.Null);

            Assert.That(carrier.TrySend(TestMessages.Int(2), acceptWithoutPeer: true),
                Is.EqualTo(SendResult.Ok));
        }
        finally
        {
            carrier.Stop();
        }
    }

    [Test]
    public void RequestValidation_ChecksTheStateBeforeTheNullPayload()
    {
        var clock = NewClock();
        using var serverHandler = new RecordingServerHandler(MemoryRental.Shared);
        StartServer(clock, serverHandler, out string name);
        var client = new RRUnreliableDirectClient(
            new StringEndPoint(name),
            TimeSpan.FromSeconds(3),
            Logger,
            MemoryRental.Shared,
            timeProvider: clock);
        using var clientHandler = new RecordingClientHandler();

        Assert.That(client.Request(null!, clientHandler), Is.EqualTo(SendResult.NotConnected),
            "a stopped client reports its state before the payload validity");

        Assert.That(client.Start(_ => { }), Is.True);
        _transports.Add(client);

        Assert.That(client.Request(null!, clientHandler), Is.EqualTo(SendResult.InvalidMessage),
            "a running client reports a null payload as invalid");
    }

    private static ManualClock NewClock()
    {
        return new ManualClock { Now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) };
    }

    /// <summary>
    /// Captures the server handler a channel currently routes inbound frames to. <see cref="Channel"/>
    /// exposes only a setter, so the installed delegate is read reflectively to invoke the exact
    /// post-stop delivery path.
    /// </summary>
    private static Action<IEndPoint, UnionDataList> CaptureServerHandler(Channel channel)
    {
        var handler = channel.ServerHandlerForTests;
        Assert.That(handler, Is.Not.Null, "the channel must have a server handler installed");
        return handler!;
    }

    [Test]
    public void SendFrame_WithANullCarrier_ReturnsErrorAndReleasesTheFrame()
    {
        var client = new RRUnreliableDirectClient(
            new StringEndPoint(NewName()),
            TimeSpan.FromSeconds(3),
            Logger,
            MemoryRental.Shared);
        UnionDataList frame = TestMessages.Int(1);

        var result = client.SendFrameForTests(frame);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(SendResult.Error));
            Assert.That(frame.IsAlive, Is.False);
        });
    }

    [Test]
    public void ConfiguredQueueCapacity_IsUsedForTheDispatchGate()
    {
        using var handler = new RecordingServerHandler(MemoryRental.Shared);
        var ten = StartServer(new ManualClock(), handler, out _, queueCapacity: 10);
        Assert.That(ten.DispatchCapacityForTests, Is.EqualTo(10));

        using var other = new RecordingServerHandler(MemoryRental.Shared);
        var twoHundred = StartServer(new ManualClock(), other, out _, queueCapacity: 200);
        Assert.That(twoHundred.DispatchCapacityForTests, Is.EqualTo(200));
    }

    private static string NewName()
    {
        return "rr-carrier-" + Guid.NewGuid().ToString("N");
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

    private RRUnreliableDirectServer StartServer(
        ManualClock clock,
        IRRUnreliableServerHandler handler,
        out string name,
        int queueCapacity = 100)
    {
        name = NewName();
        var server = new RRUnreliableDirectServer(
            name,
            TimeSpan.FromSeconds(3),
            Logger,
            MemoryRental.Shared,
            clock,
            queueCapacity);
        Assert.That(server.Init(handler), Is.True);
        Assert.That(server.Start(_ => { }), Is.True);
        _transports.Add(server);
        return server;
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
