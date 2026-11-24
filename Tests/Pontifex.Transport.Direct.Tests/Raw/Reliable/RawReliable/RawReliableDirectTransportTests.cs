using System.Collections.Concurrent;
using System.Diagnostics;
using Actuarius.Memory;
using Pontifex;
using Pontifex.Endpoints;
using Pontifex.Raw;
using Pontifex.Raw.Reliable;
using Pontifex.Raw.Reliable.Direct;
using Pontifex.StopReasons;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Tests.Raw.Reliable.RawReliable;

/// <summary>
/// Focused behavioural tests for the in-process reliable Direct transport pair: the mandatory transport
/// handshake, zero reject linger, and the ported Direct carrier lifecycle cases.
/// </summary>
[TestFixture]
public sealed class RawReliableDirectTransportTests
{
    private const int MessageMaxByteSize = 1024 * 1024;
    private const long BufferCapacityBytes = 1024 * 1024;

    private static readonly ILogger Logger = new Logger([]);

    private readonly List<ITransport> _transports = new();
    private readonly Pontifex.Tests.RR.TestSupport.ManualTimerFactory _clientHandshakeTimers = new();
    private readonly Pontifex.Tests.RR.TestSupport.ManualTimerFactory _serverHandshakeTimers = new();

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
            try
            {
                _transports[i].Stop();
            }
            catch (Exception)
            {
            }
        }

        _transports.Clear();
    }

    [Test]
    public void Successful_handshake_notifies_both_handlers()
    {
        var factory = new RecordingFactory(_ => new RecordingServerHandler());
        var server = StartServer(factory, out var serverName);

        var clientHandler = new RecordingClientHandler();
        var client = StartClient(serverName, clientHandler, waitForConnect: true);

        Assert.That(clientHandler.ConnectedCount, Is.EqualTo(1));
        Assert.That(clientHandler.Endpoint, Is.Not.Null);
        Assert.That(clientHandler.Endpoint!.IsConnected, Is.True);
        Assert.That(clientHandler.Endpoint.RemoteEndPoint, Is.Not.Null);

        var serverEndpoint = WaitForServerEndpoint(factory);
        Assert.That(serverEndpoint.IsConnected, Is.True);
        Assert.That(factory.Calls, Is.EqualTo(1));
        Assert.That(factory.LastHandler!.ConnectedCount, Is.EqualTo(1));

        Assert.That(client.MessageMaxByteSize, Is.EqualTo(MessageMaxByteSize));
        Assert.That(_clientHandshakeTimers.PendingCount, Is.Zero);
        Assert.That(_serverHandshakeTimers.PendingCount, Is.Zero);
    }

    [Test]
    public void Client_handshake_timeout_fires_before_a_response()
    {
        var serverName = NewServerName();
        Assert.That(ChannelManager.Instance.RegisterServer(RawReliableDirectInfo.TransportName, new StringEndPoint(serverName), _ => { }),
            Is.True);

        try
        {
            var handler = new RecordingClientHandler();
            var client = StartClient(serverName, handler, waitForConnect: false);

            WaitFor(() => _clientHandshakeTimers.PendingCount > 0,
                "The client did not schedule its handshake deadline");
            Assert.That(_clientHandshakeTimers.FireAll(), Is.GreaterThan(0));
            WaitFor(() => handler.StoppedReason != null, "The client did not time out");

            Assert.Multiple(() =>
            {
                Assert.That(handler.StoppedReason, Is.TypeOf<TimeOut>());
                Assert.That(handler.ConnectedCount, Is.Zero);
                Assert.That(handler.DisconnectReason, Is.Null);
                Assert.That(client.IsStarted, Is.False);
                Assert.That(client.IsValid, Is.True);
            });
        }
        finally
        {
            ChannelManager.Instance.UnregisterServer(RawReliableDirectInfo.TransportName, new StringEndPoint(serverName));
        }
    }

    [Test]
    public void Server_handshake_timeout_fires_before_a_request()
    {
        var factory = new RecordingFactory(_ => new RecordingServerHandler());
        var server = StartServer(factory, out var serverName);
        var channel = ChannelManager.Instance.Connect(RawReliableDirectInfo.TransportName, new StringEndPoint(serverName),
            new GuidEndPoint(Guid.NewGuid()));
        Assert.That(channel, Is.Not.Null);

        try
        {
            WaitFor(() => _serverHandshakeTimers.PendingCount > 0,
                "The server did not schedule its handshake deadline");
            Assert.That(_serverHandshakeTimers.FireAll(), Is.GreaterThan(0));
            WaitFor(() => channel!.IsDisposed, "The server did not close the expired handshake");

            Assert.Multiple(() =>
            {
                Assert.That(server.IsStarted, Is.True);
                Assert.That(factory.Calls, Is.Zero);
            });
        }
        finally
        {
            channel!.Dispose();
        }
    }

    [Test]
    public void Handshake_frames_are_not_delivered_to_the_handlers()
    {
        var factory = new RecordingFactory(_ => new RecordingServerHandler());
        var server = StartServer(factory, out var serverName);

        var clientHandler = new RecordingClientHandler();
        StartClient(serverName, clientHandler, waitForConnect: true);
        WaitForServerEndpoint(factory);

        Assert.That(clientHandler.ReceivedValues, Is.Empty,
            "the client handler must not receive the server handshake response");
        Assert.That(factory.LastHandler!.ReceivedValues, Is.Empty,
            "the server handler must not receive the client handshake request");
    }

    [Test]
    public void Messages_are_delivered_in_both_directions_in_order()
    {
        var factory = new RecordingFactory(_ => new RecordingServerHandler());
        var server = StartServer(factory, out var serverName);

        var clientHandler = new RecordingClientHandler();
        var client = StartClient(serverName, clientHandler, waitForConnect: true);

        var serverEndpoint = WaitForServerEndpoint(factory);
        Assert.That(serverEndpoint, Is.Not.Null);

        for (int i = 0; i < 10; i++)
        {
            Assert.That(clientHandler.Endpoint!.Send(CreateMessage(client, i)), Is.EqualTo(SendResult.Ok));
        }

        for (int i = 0; i < 10; i++)
        {
            Assert.That(serverEndpoint.Send(CreateMessage(server, 100 + i)), Is.EqualTo(SendResult.Ok));
        }

        WaitFor(() => clientHandler.ReceivedCount == 10, "The client did not receive every server message");
        WaitFor(() => factory.LastHandler!.ReceivedCount == 10,
            "The server did not receive every client message");

        Assert.That(clientHandler.ReceivedValues,
            Is.EqualTo(new[] { 100, 101, 102, 103, 104, 105, 106, 107, 108, 109 }));
        Assert.That(factory.LastHandler!.ReceivedValues,
            Is.EqualTo(new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }));
    }

    [Test]
    public void Version_mismatch_stops_the_client_with_ProtocolMismatch()
    {
        var serverName = NewServerName();
        using var fakeServer = new WrongVersionServer(serverName, RawReliableDirectInfo.HandshakeName,
            MessageMaxByteSize);

        var clientHandler = new RecordingClientHandler();
        var client = StartClient(serverName, clientHandler, waitForConnect: false);

        WaitFor(() => clientHandler.StoppedReason != null, "The mismatched client did not stop");

        Assert.That(clientHandler.ConnectedCount, Is.Zero);
        Assert.That(clientHandler.DisconnectReason, Is.Null);
        Assert.That(clientHandler.StoppedReason, Is.Not.TypeOf<UnknownRemoteIntention>(),
            "the synchronous reject path must win the stop race against the channel-dispose callback");
        Assert.That(clientHandler.StoppedReason, Is.TypeOf<ProtocolMismatch>());
        Assert.That(((ProtocolMismatch)clientHandler.StoppedReason!).Text, Does.Contain("version mismatch"));
        Assert.That(client.IsStarted, Is.False);
    }

    [Test]
    public void Name_mismatch_stops_the_client_with_ProtocolMismatch_and_skips_the_factory()
    {
        var factory = new RecordingFactory(_ => new RecordingServerHandler());
        var server = StartServer(factory, out var serverName, handshakeName: "other");

        var clientHandler = new RecordingClientHandler();
        var client = StartClient(serverName, clientHandler, waitForConnect: false);

        WaitFor(() => clientHandler.StoppedReason != null, "The mismatched client did not stop");

        Assert.That(clientHandler.ConnectedCount, Is.Zero);
        Assert.That(clientHandler.StoppedReason, Is.Not.TypeOf<UnknownRemoteIntention>(),
            "the synchronous reject path must win the stop race against the channel-dispose callback");
        Assert.That(clientHandler.StoppedReason, Is.TypeOf<ProtocolMismatch>());
        Assert.That(((ProtocolMismatch)clientHandler.StoppedReason!).Text, Does.Contain("name mismatch"));
        Assert.That(factory.Calls, Is.Zero, "a name mismatch must be rejected before the factory is invoked");
        Assert.That(client.IsStarted, Is.False);
    }

    [Test]
    public void Rejected_client_stops_with_HandshakeRejected()
    {
        var factory = new RecordingFactory(_ => null);
        var server = StartServer(factory, out var serverName);

        var clientHandler = new RecordingClientHandler();
        var client = StartClient(serverName, clientHandler, waitForConnect: false);

        WaitFor(() => clientHandler.StoppedReason != null, "The rejected client did not stop");

        Assert.That(clientHandler.ConnectedCount, Is.Zero);
        Assert.That(clientHandler.DisconnectReason, Is.Null);
        Assert.That(clientHandler.StoppedReason, Is.TypeOf<HandshakeRejected>());
        Assert.That(factory.Calls, Is.EqualTo(1));
        Assert.That(client.IsValid, Is.True);
        Assert.That(client.IsStarted, Is.False);
        Assert.That(server.IsStarted, Is.True);
    }

    [Test]
    public void Message_max_size_is_negotiated_as_the_minimum()
    {
        var factory = new RecordingFactory(_ => new RecordingServerHandler());
        var server = StartServer(factory, out var serverName, messageMaxSize: 4096);

        var clientHandler = new RecordingClientHandler();
        var client = StartClient(serverName, clientHandler, waitForConnect: true, messageMaxSize: 100_000);
        var serverEndpoint = WaitForServerEndpoint(factory);

        Assert.That(clientHandler.Endpoint!.MessageMaxByteSize, Is.EqualTo(4096),
            "the client endpoint must apply the negotiated minimum");
        Assert.That(serverEndpoint.MessageMaxByteSize, Is.EqualTo(4096),
            "the server endpoint must apply the negotiated minimum");

        var clientOversized = client.Memory.CollectablePool.Acquire<UnionDataList>();
        clientOversized.PutLast(new UnionData(client.Memory.ByteArraysPool.Acquire(4096)));
        Assert.That(clientHandler.Endpoint.Send(clientOversized), Is.EqualTo(SendResult.MessageTooBig));

        var serverOversized = server.Memory.CollectablePool.Acquire<UnionDataList>();
        serverOversized.PutLast(new UnionData(server.Memory.ByteArraysPool.Acquire(4096)));
        Assert.That(serverEndpoint.Send(serverOversized), Is.EqualTo(SendResult.MessageTooBig));

        Assert.That(clientHandler.Endpoint.Send(CreateMessage(client, 7)), Is.EqualTo(SendResult.Ok));
        WaitFor(() => factory.LastHandler!.ReceivedCount == 1,
            "the connection did not survive the oversized negotiation checks");
        Assert.That(clientHandler.Endpoint.IsConnected, Is.True);
        Assert.That(serverEndpoint.IsConnected, Is.True);
    }

    [Test]
    public void Rejected_client_closes_with_zero_linger()
    {
        var factory = new RecordingFactory(_ => null);
        var server = StartServer(factory, out var serverName);

        Assert.That(((RawReliableAdmissionServerTransport)server).RejectLinger, Is.EqualTo(TimeSpan.Zero),
            "the Direct reject linger must be exactly zero");

        var serverEndPoint = new StringEndPoint(serverName);
        var clientEndPoint = new GuidEndPoint(Guid.NewGuid());
        var channel = ChannelManager.Instance.Connect(RawReliableDirectInfo.TransportName, serverEndPoint, clientEndPoint);
        Assert.That(channel, Is.Not.Null);

        UnionDataList? rejection = null;
        bool disposed = false;
        channel!.ClientHandler = message => rejection = message;
        channel.Disposed += () => disposed = true;

        var request = RawReliableHandshake.BuildRequest(RawReliableDirectInfo.HandshakeName, MessageMaxByteSize);
        request.PutFirst(RawReliableDirectFrameTypes.HandshakeRequest);

        var started = Stopwatch.GetTimestamp();
        Assert.That(channel.SendToServer(request), Is.EqualTo(SendResult.Ok));
        var elapsed = Stopwatch.GetElapsedTime(started);

        Assert.That(disposed, Is.True, "the server must close the rejected channel synchronously");
        Assert.That(elapsed, Is.LessThan(TimeSpan.FromMilliseconds(100)),
            "a rejected Direct channel must close without a linger delay");
        Assert.That(rejection, Is.Not.Null, "the client must observe the rejection frame");

        try
        {
            Assert.That(rejection!.TryPopFirst(out byte frameType), Is.True);
            Assert.That(frameType, Is.EqualTo(RawReliableDirectFrameTypes.HandshakeReject));
            Assert.That(RawReliableHandshake.TryParseReject(rejection, out _, out _, out var status, out _),
                Is.True);
            Assert.That(status, Is.EqualTo(RawReliableHandshake.HandshakeStatus.Rejected));
        }
        finally
        {
            rejection!.Release();
        }
    }

    [Test]
    public void Direct_client_uses_and_disposes_its_handshake_timer()
    {
        var factory = new RecordingFactory(_ => new RecordingServerHandler());
        var server = StartServer(factory, out var serverName);

        var clientHandler = new RecordingClientHandler();
        var client = StartClient(serverName, clientHandler, waitForConnect: true);

        var endpoint = client.CurrentEndpoint;
        Assert.That(endpoint, Is.Not.Null);
        Assert.That(_clientHandshakeTimers.ScheduledCount, Is.GreaterThan(0),
            "the Direct client must schedule its configured deadline");
        Assert.That(_clientHandshakeTimers.PendingCount, Is.Zero,
            "a completed handshake must dispose its deadline");
        Assert.That(endpoint!.HasHandshakeTimer, Is.False,
            "no deadline may remain installed after a completed handshake");
    }

    [Test]
    public void Client_disconnect_notifies_the_server_session()
    {
        var factory = new RecordingFactory(_ => new RecordingServerHandler());
        var server = StartServer(factory, out var serverName);

        var clientHandler = new RecordingClientHandler();
        StartClient(serverName, clientHandler, waitForConnect: true);
        var serverEndpoint = WaitForServerEndpoint(factory);

        var reason = new UserIntention("test", "client disconnect");
        Assert.That(clientHandler.Endpoint!.Disconnect(reason), Is.True);

        WaitFor(() => clientHandler.StoppedReason != null, "The client did not stop");
        WaitFor(() => factory.LastHandler!.DisconnectReason != null,
            "The server session was not disconnected");

        Assert.That(clientHandler.DisconnectReason, Is.SameAs(reason));
        Assert.That(clientHandler.StoppedReason, Is.SameAs(reason));
        Assert.That(factory.LastHandler!.DisconnectReason, Is.TypeOf<UnknownRemoteIntention>());
        Assert.That(serverEndpoint.IsConnected, Is.False);
        Assert.That(server.IsStarted, Is.True);
    }

    [Test]
    public void Server_stop_notifies_connected_clients()
    {
        var factory = new RecordingFactory(_ => new RecordingServerHandler());
        var server = StartServer(factory, out var serverName);

        var clientHandler = new RecordingClientHandler();
        StartClient(serverName, clientHandler, waitForConnect: true);
        WaitForServerEndpoint(factory);

        var reason = new UserIntention("test", "server stop");
        server.Stop(reason);

        WaitFor(() => clientHandler.StoppedReason != null, "The client did not stop");

        Assert.That(factory.LastHandler!.DisconnectReason, Is.SameAs(reason));
        Assert.That(clientHandler.DisconnectReason, Is.TypeOf<UnknownRemoteIntention>());
        Assert.That(clientHandler.StoppedReason, Is.TypeOf<UnknownRemoteIntention>());
        Assert.That(server.IsStarted, Is.False);
    }

    [Test]
    public void Client_start_without_server_stops_without_connecting()
    {
        var clientHandler = new RecordingClientHandler();
        var client = StartClient(NewServerName(), clientHandler, waitForConnect: false);

        WaitFor(() => clientHandler.StoppedReason != null, "The client did not stop");

        Assert.That(clientHandler.ConnectedCount, Is.Zero);
        Assert.That(clientHandler.DisconnectReason, Is.Null);
        Assert.That(client.IsValid, Is.True);
        Assert.That(client.IsStarted, Is.False);
    }

    [Test]
    public void Oversized_message_is_rejected_and_the_connection_survives()
    {
        var factory = new RecordingFactory(_ => new RecordingServerHandler());
        var server = StartServer(factory, out var serverName);

        var clientHandler = new RecordingClientHandler();
        var client = StartClient(serverName, clientHandler, waitForConnect: true);

        var oversized = client.Memory.CollectablePool.Acquire<UnionDataList>();
        var bytes = client.Memory.ByteArraysPool.Acquire(clientHandler.Endpoint!.MessageMaxByteSize);
        oversized.PutLast(new UnionData(bytes));

        Assert.That(clientHandler.Endpoint.Send(oversized), Is.EqualTo(SendResult.MessageTooBig));
        Assert.That(clientHandler.Endpoint.IsConnected, Is.True);

        Assert.That(clientHandler.Endpoint.Send(CreateMessage(client, 7)), Is.EqualTo(SendResult.Ok));
        WaitFor(() => factory.LastHandler!.ReceivedCount == 1,
            "The connection did not survive the oversized message");
        Assert.That(factory.LastHandler!.ReceivedValues, Is.EqualTo(new[] { 7 }));
    }

    [Test]
    public void Client_transport_stop_notifies_the_handler_and_the_server_session()
    {
        var factory = new RecordingFactory(_ => new RecordingServerHandler());
        var server = StartServer(factory, out var serverName);

        var clientHandler = new RecordingClientHandler();
        var client = StartClient(serverName, clientHandler, waitForConnect: true);
        WaitForServerEndpoint(factory);

        var reason = new UserIntention("test", "client transport stop");
        client.Stop(reason);

        WaitFor(() => clientHandler.StoppedReason != null, "The client did not stop");

        Assert.That(clientHandler.DisconnectReason, Is.SameAs(reason));
        Assert.That(clientHandler.StoppedReason, Is.SameAs(reason));
        Assert.That(factory.LastHandler!.DisconnectReason, Is.TypeOf<UnknownRemoteIntention>());
        Assert.That(client.IsStarted, Is.False);
        Assert.That(server.IsStarted, Is.True);
    }

    [Test]
    public void Many_clients_connect_concurrently()
    {
        const int clientCount = 64;

        var sessionHandlers = new ConcurrentBag<RecordingServerHandler>();
        var factory = new RecordingFactory(_ =>
        {
            var handler = new RecordingServerHandler();
            sessionHandlers.Add(handler);
            return handler;
        });
        var server = StartServer(factory, out var serverName);

        var handlers = new RecordingClientHandler[clientCount];
        var started = new ConcurrentBag<RawReliableDirectClientTransport>();
        var errors = new ConcurrentBag<string>();

        Parallel.For(0, clientCount, i =>
        {
            var handler = new RecordingClientHandler();
            handlers[i] = handler;
            var client = new RawReliableDirectClientTransport(serverName, Logger, MemoryRental.Shared);
            started.Add(client);
            try
            {
                if (!client.Init(handler))
                {
                    errors.Add("init");
                    return;
                }

                if (!client.Start(_ => { }))
                {
                    errors.Add("start");
                    return;
                }
            }
            catch (Exception ex)
            {
                errors.Add(ex.Message);
            }
        });

        lock (_transports)
        {
            foreach (var client in started)
            {
                _transports.Add(client);
            }
        }

        Assert.That(errors, Is.Empty);
        WaitFor(() => handlers.All(h => h.ConnectedCount == 1), "Not every client connected");
        WaitFor(() => sessionHandlers.Count == clientCount, "Not every session was created");

        foreach (var handler in handlers)
        {
            var message = MemoryRental.Shared.CollectablePool.Acquire<UnionDataList>();
            message.PutLast(1);
            Assert.That(handler.Endpoint!.Send(message), Is.EqualTo(SendResult.Ok));
        }

        WaitFor(() => sessionHandlers.All(h => h.ReceivedCount == 1),
            "Not every session received its client message");
        Assert.That(server.IsStarted, Is.True);
    }

    [Test]
    public void ServerHandler_ReceivesOnDisconnectedThenOnStopped_OnNormalStop()
    {
        var factory = new RecordingFactory(_ => new RecordingServerHandler());
        var server = StartServer(factory, out var serverName);

        var clientHandler = new RecordingClientHandler();
        StartClient(serverName, clientHandler, waitForConnect: true);
        var serverEndpoint = WaitForServerEndpoint(factory);
        var serverHandler = factory.LastHandler!;
        WaitFor(() => serverHandler.ConnectedCount == 1, "The server session did not connect");

        var reason = new UserIntention("test", "server session stop");
        Assert.That(serverEndpoint.Disconnect(reason), Is.True);

        WaitFor(() => serverHandler.StoppedReason != null, "The server session did not stop");

        Assert.That(serverHandler.DisconnectedCount, Is.EqualTo(1));
        Assert.That(serverHandler.StoppedCount, Is.EqualTo(1));
        Assert.That(serverHandler.StoppedReason, Is.SameAs(reason));
        Assert.That(serverHandler.Events,
            Is.EqualTo(new[] { "connected", "disconnected", "stopped" }),
            "the server handler must observe the documented lifecycle order");
    }

    private static string NewServerName()
    {
        return "direct-" + Guid.NewGuid().ToString("N");
    }

    private RawReliableDirectServerTransport StartServer(
        IRawReliableServerHandlerFactory<IRawReliableServerHandler> factory,
        out string serverName,
        string handshakeName = RawReliableDirectInfo.HandshakeName,
        int? messageMaxSize = null)
    {
        serverName = NewServerName();
        var server = new RawReliableDirectServerTransport(
            serverName,
            Logger,
            MemoryRental.Shared,
            messageMaxSize,
            BufferCapacityBytes,
            handshakeName,
            RawReliableDirectInfo.DefaultHandshakeMaxByteSize,
            TimeSpan.FromSeconds(10),
            _serverHandshakeTimers);
        Assert.That(server.Init(factory), Is.True);
        Assert.That(server.Start(_ => { }), Is.True);
        _transports.Add(server);
        return server;
    }

    private RawReliableDirectClientTransport StartClient(
        string serverName,
        RecordingClientHandler handler,
        bool waitForConnect,
        int? messageMaxSize = null,
        string handshakeName = RawReliableDirectInfo.HandshakeName)
    {
        var client = new RawReliableDirectClientTransport(
            serverName,
            Logger,
            MemoryRental.Shared,
            messageMaxSize,
            BufferCapacityBytes,
            handshakeName,
            RawReliableDirectInfo.DefaultHandshakeMaxByteSize,
            TimeSpan.FromSeconds(10),
            _clientHandshakeTimers);

        Assert.That(client.Init(handler), Is.True);
        Assert.That(client.Start(_ => { }), Is.True);
        _transports.Add(client);

        if (waitForConnect)
        {
            WaitFor(() => handler.ConnectedCount == 1, "The client did not connect");
        }

        return client;
    }

    private static IRawReliableEndpoint WaitForServerEndpoint(RecordingFactory factory)
    {
        WaitFor(() => factory.LastHandler?.Endpoint != null, "The server session was not created");
        return factory.LastHandler!.Endpoint!;
    }

    [Test]
    public void Payload_at_exact_message_max_is_accepted_in_both_directions()
    {
        const int limit = 256;
        var factory = new RecordingFactory(_ => new RecordingServerHandler());
        var server = StartServer(factory, out var serverName, messageMaxSize: limit);

        var clientHandler = new RecordingClientHandler();
        var client = StartClient(serverName, clientHandler, waitForConnect: true,
            messageMaxSize: limit);
        var serverEndpoint = WaitForServerEndpoint(factory);
        WaitFor(() => factory.LastHandler!.ConnectedCount == 1, "The server session did not connect");

        UnionDataList fromClient = CreateExactSizeMessage(client, limit);
        Assert.That(fromClient.GetDataSize(), Is.EqualTo(limit));
        Assert.That(clientHandler.Endpoint!.Send(fromClient), Is.EqualTo(SendResult.Ok));

        UnionDataList fromServer = CreateExactSizeMessage(server, limit);
        Assert.That(fromServer.GetDataSize(), Is.EqualTo(limit));
        Assert.That(serverEndpoint.Send(fromServer), Is.EqualTo(SendResult.Ok));

        Assert.That(clientHandler.Endpoint.Send(CreateMessage(client, 7)), Is.EqualTo(SendResult.Ok));
        WaitFor(() => factory.LastHandler!.ReceivedCount == 1,
            "The connection did not survive the exact-limit payload");

        UnionDataList overClient = CreateExactSizeMessage(client, limit);
        overClient.PutLast((byte)1);
        UnionDataList overServer = CreateExactSizeMessage(server, limit);
        overServer.PutLast((byte)1);

        Assert.Multiple(() =>
        {
            Assert.That(clientHandler.Endpoint.Send(overClient), Is.EqualTo(SendResult.MessageTooBig));
            Assert.That(serverEndpoint.Send(overServer), Is.EqualTo(SendResult.MessageTooBig));
        });
    }

    private static UnionDataList CreateExactSizeMessage(IRawTransport transport, int limit)
    {
        var empty = transport.Memory.CollectablePool.Acquire<UnionDataList>();
        if (empty.GetDataSize() == limit)
        {
            return empty;
        }

        empty.Release();

        for (int overhead = 1; overhead <= 8; overhead++)
        {
            int byteCount = limit - overhead;
            if (byteCount <= 0)
            {
                continue;
            }

            var message = transport.Memory.CollectablePool.Acquire<UnionDataList>();
            var bytes = transport.Memory.ByteArraysPool.Acquire(byteCount);
            message.PutLast(new UnionData(bytes));
            if (message.GetDataSize() == limit)
            {
                return message;
            }

            message.Release();
        }

        throw new InvalidOperationException($"Unable to construct a UnionDataList of exactly {limit} bytes.");
    }

    private static UnionDataList CreateMessage(IRawTransport transport, int value)
    {
        var message = transport.Memory.CollectablePool.Acquire<UnionDataList>();
        message.PutLast(value);
        return message;
    }

    private static void WaitFor(Func<bool> condition, string message)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                Assert.Fail(message);
            }

            Thread.Sleep(1);
        }
    }

    /// <summary>
    /// Frame constants exposed by symbol so the tests survive frame-type renumbering.
    /// </summary>
    private abstract class RawReliableDirectFrameTypes : RawReliableEndpoint
    {
        protected RawReliableDirectFrameTypes()
            : base("frame-types", null!, null!, 1, 1, MemoryRental.Shared, new Logger([]))
        {
        }

        public static byte HandshakeRequest => (byte)MessageType.HandshakeRequestMessage;

        public static byte HandshakeResponse => (byte)MessageType.HandshakeResponseMessage;

        public static byte HandshakeReject => (byte)MessageType.HandshakeRejectMessage;
    }

    private sealed class WrongVersionServer : IDisposable
    {
        private readonly StringEndPoint _serverEndPoint;
        private readonly string _handshakeName;
        private readonly int _messageMaxByteSize;

        public WrongVersionServer(string serverName, string handshakeName, int messageMaxByteSize)
        {
            _serverEndPoint = new StringEndPoint(serverName);
            _handshakeName = handshakeName;
            _messageMaxByteSize = messageMaxByteSize;

            Assert.That(ChannelManager.Instance.RegisterServer(RawReliableDirectInfo.TransportName, _serverEndPoint, OnChannelCreated), Is.True);
        }

        private void OnChannelCreated(Channel channel)
        {
            channel.ServerHandler = (_, message) =>
            {
                try
                {
                    if (!message.TryPopFirst(out byte frameType) ||
                        frameType != RawReliableDirectFrameTypes.HandshakeRequest)
                    {
                        return;
                    }

                    var response = RawReliableHandshake.BuildResponse(
                        (short)(RawReliableHandshake.Version + 1), _handshakeName, _messageMaxByteSize);
                    response.PutFirst(RawReliableDirectFrameTypes.HandshakeResponse);
                    channel.SendToClient(response);
                }
                finally
                {
                    message.Release();
                }
            };
        }

        public void Dispose()
        {
            ChannelManager.Instance.UnregisterServer(RawReliableDirectInfo.TransportName, _serverEndPoint);
        }
    }

    private sealed class RecordingFactory : IRawReliableServerHandlerFactory<IRawReliableServerHandler>
    {
        private readonly object _lock = new();
        private readonly Func<IEndPoint, IRawReliableServerHandler?> _factory;
        private int _calls;

        public RecordingFactory(Func<IEndPoint, IRawReliableServerHandler?> factory)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        public int Calls => Volatile.Read(ref _calls);

        public RecordingServerHandler? LastHandler { get; private set; }

        public IRawReliableServerHandler? TryCreate(IEndPoint remote)
        {
            Interlocked.Increment(ref _calls);
            var handler = _factory(remote);

            // The injected delegate must return a RecordingServerHandler when the test reads LastHandler.
            if (handler is RecordingServerHandler recording)
            {
                lock (_lock)
                {
                    LastHandler = recording;
                }
            }

            return handler;
        }
    }

    private sealed class RecordingClientHandler : IRawReliableClientHandler
    {
        private readonly object _lock = new();
        private readonly List<int> _received = new();

        public IRawReliableEndpoint? Endpoint { get; private set; }

        public int ConnectedCount { get; private set; }

        public StopReason? DisconnectReason { get; private set; }

        public StopReason? StoppedReason { get; private set; }

        public int ReceivedCount
        {
            get
            {
                lock (_lock)
                {
                    return _received.Count;
                }
            }
        }

        public IReadOnlyList<int> ReceivedValues
        {
            get
            {
                lock (_lock)
                {
                    return _received.ToArray();
                }
            }
        }

        public void OnConnected(IRawReliableEndpoint endPoint)
        {
            lock (_lock)
            {
                Endpoint = endPoint;
                ConnectedCount++;
            }
        }

        public void OnReceived(UnionDataList message)
        {
            try
            {
                if (message.TryPopFirst(out int value))
                {
                    lock (_lock)
                    {
                        _received.Add(value);
                    }
                }
            }
            finally
            {
                message.Release();
            }
        }

        public void OnDisconnected(StopReason reason)
        {
            lock (_lock)
            {
                DisconnectReason = reason;
            }
        }

        public void OnStopped(StopReason reason)
        {
            lock (_lock)
            {
                StoppedReason = reason;
            }
        }
    }

    private sealed class RecordingServerHandler : IRawReliableServerHandler
    {
        private readonly object _lock = new();
        private readonly List<int> _received = new();
        private readonly List<string> _events = new();

        public IRawReliableEndpoint? Endpoint { get; private set; }

        public int ConnectedCount { get; private set; }

        public StopReason? DisconnectReason { get; private set; }

        public StopReason? StoppedReason { get; private set; }

        public int DisconnectedCount { get; private set; }

        public int StoppedCount { get; private set; }

        public IReadOnlyList<string> Events
        {
            get
            {
                lock (_lock)
                {
                    return _events.ToArray();
                }
            }
        }

        public int ReceivedCount
        {
            get
            {
                lock (_lock)
                {
                    return _received.Count;
                }
            }
        }

        public IReadOnlyList<int> ReceivedValues
        {
            get
            {
                lock (_lock)
                {
                    return _received.ToArray();
                }
            }
        }

        public void OnConnected(IRawReliableEndpoint endPoint)
        {
            lock (_lock)
            {
                Endpoint = endPoint;
                ConnectedCount++;
                _events.Add("connected");
            }
        }

        public void OnReceived(UnionDataList message)
        {
            try
            {
                if (message.TryPopFirst(out int value))
                {
                    lock (_lock)
                    {
                        _received.Add(value);
                    }
                }
            }
            finally
            {
                message.Release();
            }
        }

        public void OnDisconnected(StopReason reason)
        {
            lock (_lock)
            {
                DisconnectReason = reason;
                DisconnectedCount++;
                _events.Add("disconnected");
            }
        }

        public void OnStopped(StopReason reason)
        {
            lock (_lock)
            {
                StoppedReason = reason;
                StoppedCount++;
                _events.Add("stopped");
            }
        }
    }
}
