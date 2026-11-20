using System.Net;
using System.Net.Sockets;
using Actuarius.Memory;
using Pontifex.Raw.Reliable;
using Pontifex.Raw.Reliable.Tcp;
using Pontifex.StopReasons;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Tests.Raw.Reliable.RawReliable;

/// <summary>
/// Real loopback TCP end-to-end tests for the reliable TCP server transport, server endpoint,
/// and client transport. Both sides are the production reliable carriers, so the tests exercise the
/// complete connection lifecycle without any acknowledgement step.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><description><b>No handshake</b>: the server admits a connection as soon as the socket is accepted and the
/// handler factory admits it. There is no acknowledgement request, response, or deadline on either side.</description></item>
/// <item><description><b>Coverage</b>: connect, bidirectional order, send-in-OnConnected from both sides, rejection
/// by a null factory result, transport stop from both sides, graceful disconnect, idle watchdog, keep-alive, empty
/// and oversized messages, and a handler-initiated disconnect are covered here.</description></item>
/// </list>
/// </remarks>
[Category("Integration")]
public sealed class RawReliableTcpTransportIntegrationTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private const int MessageMaxByteSize = 1024 * 1024;
    private const long BufferCapacityBytes = 1024 * 1024;

    private static IMemoryRental Memory => MemoryRental.Shared;

    private static ILogger Log => new Logger([]);

    [TearDown]
    public void TearDown()
    {
        GC.Collect();
    }

    [Test]
    public void ServerAndClient_ConnectAndNotifyBothHandlers()
    {
        using var pair = CreateConnectedPair(out var serverHandler, out var clientHandler);

        Assert.That(pair.Factory.Calls, Is.EqualTo(1),
            "the server factory must be invoked once for the admitted connection");
        Assert.That(pair.Factory.Remotes, Has.Count.EqualTo(1),
            "the server factory must observe the remote endpoint");
        Assert.That(pair.Factory.Remotes[0], Is.Not.Null);

        Assert.That(serverHandler.ConnectedCount, Is.EqualTo(1),
            "the server handler must observe the logical connection");
        Assert.That(serverHandler.Endpoint, Is.Not.Null,
            "the server handler must be given the session endpoint");
        Assert.That(clientHandler.ConnectedCount, Is.EqualTo(1),
            "the client handler must observe the logical connection");
        Assert.That(clientHandler.Endpoint, Is.Not.Null,
            "the client handler must be given the client endpoint");
    }

    [Test]
    public void BidirectionalMessages_AreDeliveredInOrder()
    {
        using var pair = CreateConnectedPair(out var serverHandler, out var clientHandler);

        for (int i = 0; i < 10; i++)
        {
            SendValue(serverHandler.Endpoint!, 100 + i);
            SendValue(clientHandler.Endpoint!, 200 + i);
        }

        WaitFor(() => serverHandler.ReceivedValues.Count == 10,
            "the server handler must receive every client message");
        WaitFor(() => clientHandler.ReceivedValues.Count == 10,
            "the client handler must receive every server message");

        Assert.That(serverHandler.ReceivedValues,
            Is.EqualTo(new[] { 200, 201, 202, 203, 204, 205, 206, 207, 208, 209 }),
            "the server handler must receive the client messages in order");
        Assert.That(clientHandler.ReceivedValues,
            Is.EqualTo(new[] { 100, 101, 102, 103, 104, 105, 106, 107, 108, 109 }),
            "the client handler must receive the server messages in order");
    }

    [Test]
    public void ServerSendImmediatelyInOnConnected_ReachesClientAfterClientOnConnected()
    {
        using var pair = CreateConnectedPair(
            out var serverHandler,
            out var clientHandler,
            configureServerHandler: handler => handler.SendOnConnectedValue = 77);

        WaitFor(() => clientHandler.ReceivedValues.Count == 1,
            "the client handler must receive the message sent from the server's OnConnected");
        Assert.That(clientHandler.ReceivedValues[0], Is.EqualTo(77));
        Assert.That(clientHandler.ReceivedBeforeConnected, Is.False,
            "the inbound payload must not overtake the client's OnConnected notification");
        Assert.That(clientHandler.Events,
            Is.EqualTo(new[] { "connected" }),
            "the client handler must observe OnConnected before any payload");
        Assert.That(serverHandler.ConnectedCount, Is.EqualTo(1));
    }

    [Test]
    public void ClientSendImmediatelyInOnConnected_ReachesServer()
    {
        using var pair = CreateConnectedPair(
            out var serverHandler,
            out var clientHandler,
            configureClientHandler: handler => handler.SendOnConnectedValue = 55);

        WaitFor(() => serverHandler.ReceivedValues.Count == 1,
            "the server handler must receive the message sent from the client's OnConnected");
        Assert.That(serverHandler.ReceivedValues[0], Is.EqualTo(55));
        Assert.That(clientHandler.ConnectedCount, Is.EqualTo(1));
    }

    [Test]
    public void FactoryReturnsNull_RejectsConnection()
    {
        var factory = new RecordingRawReliableServerHandlerFactory(_ => null);
        var clientHandler = new RecordingRawReliableClientHandler();
        using var pair = StartPair(factory, clientHandler);

        Assert.That(clientHandler.Stopped.Wait(Timeout), Is.True,
            "the client must observe the stop after the server rejects the connection");
        Assert.That(factory.Calls, Is.EqualTo(1),
            "the server factory must be asked once before the connection is rejected");
        Assert.That(factory.Handlers, Is.Empty,
            "a rejected connection must not create a session handler");
        Assert.That(clientHandler.StoppedCount, Is.EqualTo(1),
            "the client must deliver exactly one terminal notification");
    }

    [Test]
    public void ServerStop_DisconnectsSessions()
    {
        using var pair = CreateConnectedPair(out var serverHandler, out var clientHandler);

        Assert.That(pair.Server.Stop(), Is.True, "the server stop request must be accepted");

        Assert.That(serverHandler.Stopped.Wait(Timeout), Is.True,
            "the server session handler must observe the stop after the server stops");
        Assert.That(serverHandler.Events,
            Is.EqualTo(new[] { "connected", "disconnected", "stopped" }),
            "the server session must deliver the documented lifecycle");
        Assert.That(serverHandler.DisconnectedReason, Is.Not.Null);
        Assert.That(serverHandler.StoppedReason, Is.Not.Null);

        Assert.That(clientHandler.Stopped.Wait(Timeout), Is.True,
            "the client must observe the stop after the server stops");
        Assert.That(clientHandler.Events,
            Is.EqualTo(new[] { "connected", "disconnected", "stopped" }),
            "the client must deliver the documented lifecycle");
    }

    [Test]
    public void ClientStop_DisconnectsSession()
    {
        using var pair = CreateConnectedPair(out var serverHandler, out var clientHandler);

        Assert.That(pair.Client.Stop(), Is.True, "the client stop request must be accepted");

        Assert.That(serverHandler.Stopped.Wait(Timeout), Is.True,
            "the server session handler must observe the stop after the client stops");
        Assert.That(serverHandler.ConnectedCount, Is.EqualTo(1));
        Assert.That(serverHandler.DisconnectedCount, Is.EqualTo(1));
        Assert.That(serverHandler.StoppedCount, Is.EqualTo(1));

        Assert.That(clientHandler.Stopped.Wait(Timeout), Is.True,
            "the client handler must observe the stop");
        Assert.That(clientHandler.Events,
            Is.EqualTo(new[] { "connected", "disconnected", "stopped" }));
    }

    [Test]
    public void GracefulDisconnect_FromClient_SendsDisconnectFrame()
    {
        using var pair = CreateConnectedPair(out var serverHandler, out var clientHandler);

        var reason = new UserIntention("integration-test", "client graceful disconnect");
        Assert.That(clientHandler.Endpoint!.Disconnect(reason), Is.True,
            "the client endpoint must accept a graceful disconnect request");

        Assert.That(serverHandler.Stopped.Wait(Timeout), Is.True,
            "the server session must stop when the client disconnects gracefully");
        Assert.That(serverHandler.DisconnectedCount, Is.EqualTo(1));
        Assert.That(serverHandler.StoppedCount, Is.EqualTo(1));
        Assert.That(serverHandler.StoppedReason, Is.Not.Null,
            "the server must observe a stop reason derived from the disconnect frame");

        Assert.That(clientHandler.Stopped.Wait(Timeout), Is.True,
            "the client handler must observe its own stop");
        Assert.That(clientHandler.DisconnectedCount, Is.EqualTo(1));
        Assert.That(clientHandler.StoppedCount, Is.EqualTo(1));
    }

    [Test]
    public void IdleClient_IsStoppedByServerWatchdog()
    {
        using var pair = CreateConnectedPair(
            out var serverHandler,
            out var clientHandler,
            serverDisconnectTimeout: TimeSpan.FromMilliseconds(400),
            serverTickPeriodMs: 50,
            clientPingPeriod: TimeSpan.Zero);

        Assert.That(serverHandler.Stopped.Wait(Timeout), Is.True,
            "the server watchdog must stop an idle session");
        Assert.That(serverHandler.StoppedReason, Is.TypeOf<TimeOut>(),
            "the server session must stop with the idle timeout reason");
        Assert.That(serverHandler.DisconnectedCount, Is.EqualTo(1));

        Assert.That(clientHandler.Stopped.Wait(Timeout), Is.True,
            "the client must observe the stop when the server drops the idle session");
    }

    [Test]
    public void KeepAlive_KeepsConnectionAlive()
    {
        using var pair = CreateConnectedPair(
            out var serverHandler,
            out var clientHandler,
            serverDisconnectTimeout: TimeSpan.FromMilliseconds(800),
            serverTickPeriodMs: 50,
            clientPingPeriod: TimeSpan.FromMilliseconds(200));

        Thread.Sleep(TimeSpan.FromMilliseconds(2000));

        Assert.That(serverHandler.StoppedCount, Is.EqualTo(0),
            "keep-alive pings must prevent the server idle watchdog from stopping the session");
        Assert.That(clientHandler.StoppedCount, Is.EqualTo(0),
            "keep-alive must keep the client running");
        Assert.That(pair.Server.IsStarted, Is.True);
        Assert.That(pair.Client.IsStarted, Is.True);
    }

    [Test]
    public void EmptyMessage_IsDelivered()
    {
        using var pair = CreateConnectedPair(out var serverHandler, out var clientHandler);

        var message = Memory.CollectablePool.Acquire<UnionDataList>();
        Assert.That(clientHandler.Endpoint!.Send(message), Is.EqualTo(SendResult.Ok),
            "a connected endpoint must admit an empty payload");

        WaitFor(() => serverHandler.EmptyMessageCount == 1,
            "the server handler must receive the empty payload");
    }

    [Test]
    public void OversizedMessage_ReturnsMessageTooBig()
    {
        using var pair = CreateConnectedPair(
            out _,
            out var clientHandler,
            clientMessageMaxSize: 1024);

        var message = Memory.CollectablePool.Acquire<UnionDataList>();
        for (int i = 0; i < 1000; i++)
        {
            message.PutLast(new UnionData(i));
        }

        Assert.That(clientHandler.Endpoint!.Send(message), Is.EqualTo(SendResult.MessageTooBig),
            "a payload above the transport limit must be rejected synchronously");
        Assert.That(clientHandler.StoppedCount, Is.EqualTo(0),
            "a rejected oversized payload must not affect the connection");
    }

    [Test]
    public void ServerHandlerDisconnectsInOnConnected_StopsCleanly()
    {
        using var pair = CreateConnectedPair(
            out var serverHandler,
            out var clientHandler,
            configureServerHandler: handler => handler.DisconnectOnConnected = true,
            serverGracefulShutdownDeadline: TimeSpan.FromSeconds(2));

        Assert.That(serverHandler.Stopped.Wait(Timeout), Is.True,
            "a handler-initiated disconnect must stop the server session");
        Assert.That(serverHandler.StoppedReason, Is.TypeOf<UserIntention>(),
            "the handler-initiated disconnect must not become a receiver-start failure");
        Assert.That(serverHandler.ConnectedCount, Is.EqualTo(1));
        Assert.That(serverHandler.DisconnectedCount, Is.EqualTo(1));

        Assert.That(clientHandler.Stopped.Wait(Timeout), Is.True,
            "the client must observe the disconnect initiated by the server handler");
        Assert.That(clientHandler.DisconnectedCount, Is.EqualTo(1));
        Assert.That(clientHandler.StoppedCount, Is.EqualTo(1));
    }

    #region Helpers

    private static int GetFreeLoopbackPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static Pair StartPair(
        RecordingRawReliableServerHandlerFactory factory,
        RecordingRawReliableClientHandler clientHandler,
        TimeSpan? serverDisconnectTimeout = null,
        int serverTickPeriodMs = 100,
        TimeSpan? clientPingPeriod = null,
        int clientMessageMaxSize = MessageMaxByteSize,
        TimeSpan? clientDisconnectTimeout = null,
        TimeSpan? serverGracefulShutdownDeadline = null)
    {
        int port = GetFreeLoopbackPort();

        var server = new RawReliableTcpServerTransport(
            IPAddress.Loopback,
            port,
            10,
            serverDisconnectTimeout ?? TimeSpan.FromSeconds(30),
            MessageMaxByteSize,
            Log,
            Memory,
            bufferCapacityBytes: BufferCapacityBytes,
            tickPeriodMs: serverTickPeriodMs,
            gracefulShutdownDeadline: serverGracefulShutdownDeadline);

        var client = new RawReliableTcpClientTransport(
            IPAddress.Loopback,
            port,
            clientDisconnectTimeout ?? TimeSpan.FromSeconds(30),
            clientMessageMaxSize,
            Log,
            Memory,
            pingPeriod: clientPingPeriod,
            bufferCapacityBytes: BufferCapacityBytes);

        var pair = new Pair(server, client, factory, clientHandler);
        try
        {
            Assert.That(server.Init(factory), Is.True, "the server initialization must succeed");
            Assert.That(server.Start(_ => { }), Is.True, "the server start request must be accepted");
            Assert.That(client.Init(clientHandler), Is.True, "the client initialization must succeed");
            Assert.That(client.Start(_ => { }), Is.True, "the client start request must be accepted");
            return pair;
        }
        catch
        {
            pair.Dispose();
            throw;
        }
    }

    private static Pair CreateConnectedPair(
        out RecordingRawReliableServerHandler serverHandler,
        out RecordingRawReliableClientHandler clientHandler,
        Action<RecordingRawReliableServerHandler>? configureServerHandler = null,
        Action<RecordingRawReliableClientHandler>? configureClientHandler = null,
        TimeSpan? serverDisconnectTimeout = null,
        int serverTickPeriodMs = 100,
        TimeSpan? clientPingPeriod = null,
        int clientMessageMaxSize = MessageMaxByteSize,
        TimeSpan? clientDisconnectTimeout = null,
        TimeSpan? serverGracefulShutdownDeadline = null)
    {
        var createdServerHandler = new RecordingRawReliableServerHandler();
        configureServerHandler?.Invoke(createdServerHandler);

        var createdClientHandler = new RecordingRawReliableClientHandler();
        configureClientHandler?.Invoke(createdClientHandler);

        var factory = new RecordingRawReliableServerHandlerFactory(_ => createdServerHandler);
        var pair = StartPair(
            factory,
            createdClientHandler,
            serverDisconnectTimeout,
            serverTickPeriodMs,
            clientPingPeriod,
            clientMessageMaxSize,
            clientDisconnectTimeout,
            serverGracefulShutdownDeadline);

        if (!createdServerHandler.Connected.Wait(Timeout))
        {
            pair.Dispose();
            throw new AssertionException("The server handler did not observe the logical connection");
        }

        if (!createdClientHandler.Connected.Wait(Timeout))
        {
            pair.Dispose();
            throw new AssertionException("The client handler did not observe the logical connection");
        }

        serverHandler = createdServerHandler;
        clientHandler = createdClientHandler;
        return pair;
    }

    private static void SendValue(IRawReliableEndpoint endpoint, int value)
    {
        var message = Memory.CollectablePool.Acquire<UnionDataList>();
        message.PutLast(new UnionData(value));
        Assert.That(endpoint.Send(message), Is.EqualTo(SendResult.Ok),
            "a connected endpoint must admit a payload message");
    }

    private static void WaitFor(Func<bool> condition, string message)
    {
        DateTime deadline = DateTime.UtcNow + Timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return;
            }

            Thread.Sleep(10);
        }

        Assert.Fail(message);
    }

    private static void StopQuietly(ITransport transport)
    {
        try
        {
            transport.Stop();
        }
        catch (Exception)
        {
            // best-effort cleanup
        }
    }

    private sealed class Pair : IDisposable
    {
        public Pair(
            RawReliableTcpServerTransport server,
            RawReliableTcpClientTransport client,
            RecordingRawReliableServerHandlerFactory factory,
            RecordingRawReliableClientHandler clientHandler)
        {
            Server = server;
            Client = client;
            Factory = factory;
            ClientHandler = clientHandler;
        }

        public RawReliableTcpServerTransport Server { get; }

        public RawReliableTcpClientTransport Client { get; }

        public RecordingRawReliableServerHandlerFactory Factory { get; }

        public RecordingRawReliableClientHandler ClientHandler { get; }

        public void Dispose()
        {
            StopQuietly(Client);
            StopQuietly(Server);
        }
    }

    private sealed class RecordingRawReliableServerHandlerFactory
        : IRawReliableServerHandlerFactory<IRawReliableServerHandler>
    {
        private readonly object _lock = new();
        private readonly Func<IEndPoint, IRawReliableServerHandler?> _create;
        private readonly List<IEndPoint> _remotes = new();
        private readonly List<IRawReliableServerHandler> _handlers = new();

        public RecordingRawReliableServerHandlerFactory(Func<IEndPoint, IRawReliableServerHandler?> create)
        {
            _create = create;
        }

        public int Calls
        {
            get
            {
                lock (_lock)
                {
                    return _remotes.Count;
                }
            }
        }

        public IReadOnlyList<IEndPoint> Remotes
        {
            get
            {
                lock (_lock)
                {
                    return _remotes.ToArray();
                }
            }
        }

        public IReadOnlyList<IRawReliableServerHandler> Handlers
        {
            get
            {
                lock (_lock)
                {
                    return _handlers.ToArray();
                }
            }
        }

        public IRawReliableServerHandler? TryCreate(IEndPoint remote)
        {
            IRawReliableServerHandler? handler = _create(remote);
            lock (_lock)
            {
                _remotes.Add(remote);
                if (handler != null)
                {
                    _handlers.Add(handler);
                }
            }

            return handler;
        }
    }

    private sealed class RecordingRawReliableServerHandler : IRawReliableServerHandler
    {
        private readonly object _lock = new();
        private readonly List<int> _receivedValues = new();
        private readonly List<string> _events = new();

        public readonly ManualResetEventSlim Connected = new(false);
        public readonly ManualResetEventSlim Stopped = new(false);

        public int ConnectedCount { get; private set; }

        public int DisconnectedCount { get; private set; }

        public int StoppedCount { get; private set; }

        public int EmptyMessageCount { get; private set; }

        public bool ReceivedBeforeConnected { get; private set; }

        public IRawReliableEndpoint? Endpoint { get; private set; }

        public StopReason? DisconnectedReason { get; private set; }

        public StopReason? StoppedReason { get; private set; }

        public bool DisconnectOnConnected { get; set; }

        public int? SendOnConnectedValue { get; set; }

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

        public IReadOnlyList<int> ReceivedValues
        {
            get
            {
                lock (_lock)
                {
                    return _receivedValues.ToArray();
                }
            }
        }

        public void OnConnected(IRawReliableEndpoint endPoint)
        {
            lock (_lock)
            {
                ConnectedCount++;
                Endpoint = endPoint;
                _events.Add("connected");
            }

            Connected.Set();

            if (SendOnConnectedValue.HasValue)
            {
                SendValue(endPoint, SendOnConnectedValue.Value);
            }

            if (DisconnectOnConnected)
            {
                endPoint.Disconnect(new UserIntention("handler-disconnect"));
            }
        }

        public void OnReceived(UnionDataList receivedBuffer)
        {
            try
            {
                lock (_lock)
                {
                    if (ConnectedCount == 0)
                    {
                        ReceivedBeforeConnected = true;
                    }

                    if (receivedBuffer.Elements.Count == 0)
                    {
                        EmptyMessageCount++;
                    }
                    else
                    {
                        _receivedValues.Add(receivedBuffer.Elements[0].Alias.IntValue);
                    }
                }
            }
            finally
            {
                receivedBuffer.Release();
            }
        }

        public void OnDisconnected(StopReason reason)
        {
            lock (_lock)
            {
                DisconnectedCount++;
                DisconnectedReason = reason;
                _events.Add("disconnected");
            }
        }

        public void OnStopped(StopReason reason)
        {
            lock (_lock)
            {
                StoppedCount++;
                StoppedReason = reason;
                _events.Add("stopped");
            }

            Stopped.Set();
        }
    }

    private sealed class RecordingRawReliableClientHandler : IRawReliableClientHandler
    {
        private readonly object _lock = new();
        private readonly List<int> _receivedValues = new();
        private readonly List<string> _events = new();

        public readonly ManualResetEventSlim Connected = new(false);
        public readonly ManualResetEventSlim Stopped = new(false);

        public int ConnectedCount { get; private set; }

        public int DisconnectedCount { get; private set; }

        public int StoppedCount { get; private set; }

        public bool ReceivedBeforeConnected { get; private set; }

        public IRawReliableEndpoint? Endpoint { get; private set; }

        public StopReason? DisconnectedReason { get; private set; }

        public StopReason? StoppedReason { get; private set; }

        public int? SendOnConnectedValue { get; set; }

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

        public IReadOnlyList<int> ReceivedValues
        {
            get
            {
                lock (_lock)
                {
                    return _receivedValues.ToArray();
                }
            }
        }

        public void OnConnected(IRawReliableEndpoint endPoint)
        {
            lock (_lock)
            {
                ConnectedCount++;
                Endpoint = endPoint;
                _events.Add("connected");
            }

            Connected.Set();

            if (SendOnConnectedValue.HasValue)
            {
                SendValue(endPoint, SendOnConnectedValue.Value);
            }
        }

        public void OnReceived(UnionDataList receivedBuffer)
        {
            try
            {
                lock (_lock)
                {
                    if (ConnectedCount == 0)
                    {
                        ReceivedBeforeConnected = true;
                    }

                    if (receivedBuffer.Elements.Count > 0)
                    {
                        _receivedValues.Add(receivedBuffer.Elements[0].Alias.IntValue);
                    }
                }
            }
            finally
            {
                receivedBuffer.Release();
            }
        }

        public void OnDisconnected(StopReason reason)
        {
            lock (_lock)
            {
                DisconnectedCount++;
                DisconnectedReason = reason;
                _events.Add("disconnected");
            }
        }

        public void OnStopped(StopReason reason)
        {
            lock (_lock)
            {
                StoppedCount++;
                StoppedReason = reason;
                _events.Add("stopped");
            }

            Stopped.Set();
        }
    }

    #endregion
}
