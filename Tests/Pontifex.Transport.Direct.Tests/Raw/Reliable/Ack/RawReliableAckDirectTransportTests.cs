using System.Collections.Concurrent;
using Actuarius.Memory;
using Pontifex.Endpoints;
using Pontifex.Raw;
using Pontifex.Raw.Reliable;
using Pontifex.Raw.Reliable.Ack;
using Pontifex.Raw.Reliable.Ack.Direct;
using Pontifex.StopReasons;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Tests.Raw.Reliable.Ack;

/// <summary>
/// Focused behavioural tests for the new-style in-process reliable acknowledgement transport pair.
/// </summary>
[TestFixture]
public sealed class RawReliableAckDirectTransportTests
{
    private static readonly ILogger Logger = new Logger([]);

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
    public void Successful_handshake_carries_ack_data_and_response()
    {
        var acknowledger = new TestAcknowledger { Factory = (_, _) => new RecordingServerHandler { AckResponseValue = 456 } };
        var server = StartServer(acknowledger, out var serverName);

        var clientHandler = new RecordingClientHandler { AckDataValue = 123 };
        var client = StartClient(serverName, clientHandler, waitForConnect: true);

        Assert.That(clientHandler.Connected, Is.True);
        Assert.That(clientHandler.AckResponseValue, Is.EqualTo(456));
        Assert.That(acknowledger.CallCount, Is.EqualTo(1));
        Assert.That(acknowledger.ReceivedAckValue, Is.EqualTo(123));
        Assert.That(clientHandler.Endpoint, Is.Not.Null);
        Assert.That(clientHandler.Endpoint!.IsConnected, Is.True);
        Assert.That(clientHandler.Endpoint.RemoteEndPoint, Is.Not.Null);
        Assert.That(client.MessageMaxByteSize, Is.EqualTo(server.MessageMaxByteSize));
        Assert.That(clientHandler.Endpoint.MessageMaxByteSize, Is.EqualTo(client.MessageMaxByteSize));
    }

    [Test]
    public void Messages_are_delivered_in_both_directions_in_order()
    {
        var acknowledger = new TestAcknowledger { Factory = (_, _) => new RecordingServerHandler() };
        var server = StartServer(acknowledger, out var serverName);

        var clientHandler = new RecordingClientHandler();
        var client = StartClient(serverName, clientHandler, waitForConnect: true);

        var serverEndpoint = WaitForServerEndpoint(acknowledger);
        Assert.That(serverEndpoint, Is.Not.Null);

        for (int i = 0; i < 10; i++)
        {
            Assert.That(clientHandler.Endpoint!.Send(CreateMessage(client, i)), Is.EqualTo(SendResult.Ok));
        }

        for (int i = 0; i < 10; i++)
        {
            Assert.That(serverEndpoint!.Send(CreateMessage(server, 100 + i)), Is.EqualTo(SendResult.Ok));
        }

        WaitFor(() => clientHandler.ReceivedCount == 10, "The client did not receive every server message");
        WaitFor(() => acknowledger.LastHandler!.ReceivedCount == 10,
            "The server did not receive every client message");

        Assert.That(clientHandler.ReceivedValues, Is.EqualTo(new[] { 100, 101, 102, 103, 104, 105, 106, 107, 108, 109 }));
        Assert.That(acknowledger.LastHandler!.ReceivedValues, Is.EqualTo(new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }));
    }

    [Test]
    public void Rejected_client_stops_without_connecting()
    {
        var acknowledger = new TestAcknowledger { Factory = (_, _) => null };
        var server = StartServer(acknowledger, out var serverName);

        var clientHandler = new RecordingClientHandler();
        var client = StartClient(serverName, clientHandler, waitForConnect: false);

        WaitFor(() => clientHandler.StoppedReason != null, "The rejected client did not stop");

        Assert.That(clientHandler.Connected, Is.False);
        Assert.That(clientHandler.DisconnectReason, Is.Null);
        Assert.That(clientHandler.StoppedReason, Is.TypeOf<AckRejected>());
        Assert.That(client.IsValid, Is.True);
        Assert.That(client.IsStarted, Is.False);
    }

    [Test]
    public void Client_disconnect_notifies_the_server_session()
    {
        var acknowledger = new TestAcknowledger { Factory = (_, _) => new RecordingServerHandler() };
        var server = StartServer(acknowledger, out var serverName);

        var clientHandler = new RecordingClientHandler();
        StartClient(serverName, clientHandler, waitForConnect: true);
        var serverEndpoint = WaitForServerEndpoint(acknowledger);

        var reason = new UserIntention("test", "client disconnect");
        Assert.That(clientHandler.Endpoint!.Disconnect(reason), Is.True);

        WaitFor(() => clientHandler.StoppedReason != null, "The client did not stop");
        WaitFor(() => acknowledger.LastHandler!.DisconnectReason != null,
            "The server session was not disconnected");

        Assert.That(clientHandler.DisconnectReason, Is.SameAs(reason));
        Assert.That(clientHandler.StoppedReason, Is.SameAs(reason));
        Assert.That(acknowledger.LastHandler!.DisconnectReason, Is.TypeOf<UnknownRemoteIntention>());
        Assert.That(serverEndpoint!.IsConnected, Is.False);
        Assert.That(server.IsStarted, Is.True);
    }

    [Test]
    public void Server_stop_notifies_connected_clients()
    {
        var acknowledger = new TestAcknowledger { Factory = (_, _) => new RecordingServerHandler() };
        var server = StartServer(acknowledger, out var serverName);

        var clientHandler = new RecordingClientHandler();
        StartClient(serverName, clientHandler, waitForConnect: true);
        WaitForServerEndpoint(acknowledger);

        var reason = new UserIntention("test", "server stop");
        server.Stop(reason);

        WaitFor(() => clientHandler.StoppedReason != null, "The client did not stop");

        Assert.That(acknowledger.LastHandler!.DisconnectReason, Is.SameAs(reason));
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

        Assert.That(clientHandler.Connected, Is.False);
        Assert.That(clientHandler.DisconnectReason, Is.Null);
        Assert.That(client.IsValid, Is.True);
        Assert.That(client.IsStarted, Is.False);
    }

    [Test]
    public void Oversized_message_is_rejected_and_the_connection_survives()
    {
        var acknowledger = new TestAcknowledger { Factory = (_, _) => new RecordingServerHandler() };
        var server = StartServer(acknowledger, out var serverName);

        var clientHandler = new RecordingClientHandler();
        var client = StartClient(serverName, clientHandler, waitForConnect: true);

        var oversized = client.Memory.CollectablePool.Acquire<UnionDataList>();
        var bytes = client.Memory.ByteArraysPool.Acquire(client.MessageMaxByteSize);
        oversized.PutLast(new UnionData(bytes));

        Assert.That(clientHandler.Endpoint!.Send(oversized), Is.EqualTo(SendResult.MessageTooBig));
        Assert.That(clientHandler.Endpoint.IsConnected, Is.True);

        Assert.That(clientHandler.Endpoint.Send(CreateMessage(client, 7)), Is.EqualTo(SendResult.Ok));
        WaitFor(() => acknowledger.LastHandler!.ReceivedCount == 1,
            "The connection did not survive the oversized message");
        Assert.That(acknowledger.LastHandler!.ReceivedValues, Is.EqualTo(new[] { 7 }));
    }

    [Test]
    public void Client_transport_stop_notifies_the_handler_and_the_server_session()
    {
        var acknowledger = new TestAcknowledger { Factory = (_, _) => new RecordingServerHandler() };
        var server = StartServer(acknowledger, out var serverName);

        var clientHandler = new RecordingClientHandler();
        var client = StartClient(serverName, clientHandler, waitForConnect: true);
        WaitForServerEndpoint(acknowledger);

        var reason = new UserIntention("test", "client transport stop");
        client.Stop(reason);

        WaitFor(() => clientHandler.StoppedReason != null, "The client did not stop");

        Assert.That(clientHandler.DisconnectReason, Is.SameAs(reason));
        Assert.That(clientHandler.StoppedReason, Is.SameAs(reason));
        Assert.That(acknowledger.LastHandler!.DisconnectReason, Is.TypeOf<UnknownRemoteIntention>());
        Assert.That(client.IsStarted, Is.False);
        Assert.That(server.IsStarted, Is.True);
    }

    [Test]
    public void Many_clients_connect_concurrently()
    {
        const int clientCount = 64;

        var sessionHandlers = new ConcurrentBag<RecordingServerHandler>();
        var acknowledger = new TestAcknowledger
        {
            Factory = (_, _) =>
            {
                var handler = new RecordingServerHandler();
                sessionHandlers.Add(handler);
                return handler;
            }
        };
        var server = StartServer(acknowledger, out var serverName);

        var handlers = new RecordingClientHandler[clientCount];
        var started = new ConcurrentBag<RawReliableAckDirectClientTransport>();
        var errors = new ConcurrentBag<string>();

        Parallel.For(0, clientCount, i =>
        {
            var handler = new RecordingClientHandler();
            handlers[i] = handler;
            var client = new RawReliableAckDirectClientTransport(serverName, Logger, MemoryRental.Shared);
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
        WaitFor(() => handlers.All(h => h.Connected), "Not every client connected");
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

    private static string NewServerName()
    {
        return "direct-" + Guid.NewGuid().ToString("N");
    }

    private RawReliableAckDirectServerTransport StartServer(TestAcknowledger acknowledger, out string serverName)
    {
        serverName = NewServerName();
        var server = new RawReliableAckDirectServerTransport(serverName, Logger, MemoryRental.Shared);
        Assert.That(server.Init(acknowledger), Is.True);
        Assert.That(server.Start(_ => { }), Is.True);
        _transports.Add(server);
        return server;
    }

    private RawReliableAckDirectClientTransport StartClient(
        string serverName,
        RecordingClientHandler handler,
        bool waitForConnect)
    {
        var client = new RawReliableAckDirectClientTransport(serverName, Logger, MemoryRental.Shared);

        Assert.That(client.Init(handler), Is.True);

        Assert.That(client.Start(_ => { }), Is.True);
        _transports.Add(client);

        if (waitForConnect)
        {
            WaitFor(() => handler.Connected, "The client did not connect");
        }

        return client;
    }

    private static IRawReliableEndpoint? WaitForServerEndpoint(TestAcknowledger acknowledger)
    {
        WaitFor(() => acknowledger.LastHandler?.Endpoint != null, "The server session was not created");
        return acknowledger.LastHandler!.Endpoint;
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

    private sealed class TestAcknowledger : IRawReliableAckServerAcknowledger<IRawReliableAckServerHandler>
    {
        private readonly object _lock = new();

        public Func<IEndPoint, UnionDataList, RecordingServerHandler?> Factory { get; init; } =
            (_, _) => new RecordingServerHandler();

        public int CallCount { get; private set; }

        public int? ReceivedAckValue { get; private set; }

        public RecordingServerHandler? LastHandler { get; private set; }

        public IRawReliableAckServerHandler? TryAck(IEndPoint remote, UnionDataList ackData)
        {
            try
            {
                lock (_lock)
                {
                    CallCount++;
                    if (ackData.TryPopFirst(out int value))
                    {
                        ReceivedAckValue = value;
                        ackData.PutFirst(value);
                    }
                }

                var handler = Factory(remote, ackData);
                lock (_lock)
                {
                    LastHandler = handler;
                }

                return handler;
            }
            finally
            {
                ackData.Release();
            }
        }
    }

    private sealed class RecordingClientHandler : IRawReliableAckClientHandler
    {
        private readonly object _lock = new();
        private readonly List<int> _received = new();

        public IRawReliableEndpoint? Endpoint { get; private set; }

        public bool Connected { get; private set; }

        public int? AckDataValue { get; init; }

        public int? AckResponseValue { get; private set; }

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

        public void FillAckData(UnionDataList ackData)
        {
            if (AckDataValue.HasValue)
            {
                ackData.PutLast(AckDataValue.Value);
            }
        }

        public void OnConnected(IRawReliableEndpoint endPoint, UnionDataList ackResponse)
        {
            try
            {
                Endpoint = endPoint;
                Connected = true;
                if (ackResponse.TryPopFirst(out int value))
                {
                    AckResponseValue = value;
                }
            }
            finally
            {
                ackResponse.Release();
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
                Connected = false;
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

    private sealed class RecordingServerHandler : IRawReliableAckServerHandler
    {
        private readonly object _lock = new();
        private readonly List<int> _received = new();

        public IRawReliableEndpoint? Endpoint { get; private set; }

        public bool Connected { get; private set; }

        public int? AckResponseValue { get; init; }

        public StopReason? DisconnectReason { get; private set; }

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

        public void FillAckResponse(UnionDataList ackData)
        {
            if (AckResponseValue.HasValue)
            {
                ackData.PutLast(AckResponseValue.Value);
            }
        }

        public void OnConnected(IRawReliableEndpoint endPoint)
        {
            Endpoint = endPoint;
            Connected = true;
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
                Connected = false;
            }
        }

        public void OnStopped(StopReason reason)
        {
        }
    }
}
