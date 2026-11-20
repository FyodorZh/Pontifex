using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Actuarius.Memory;
using Pontifex.NetSockets;
using Pontifex.NetSockets.Tcp;
using Pontifex.Raw.Reliable;
using Pontifex.Raw.Reliable.Tcp;
using Pontifex.StopReasons;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Tests.Raw.Reliable.RawReliable;

/// <summary>
/// Real loopback TCP integration tests for the mandatory transport handshake: version and name mismatches,
/// factory rejection, timeout, message-maximum negotiation, reject linger, capacity rejection, and the
/// pending-response failure path.
/// </summary>
[Category("Integration")]
public sealed class RawReliableTcpHandshakeIntegrationTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private const int MessageMaxByteSize = 1024 * 1024;
    private const long BufferCapacityBytes = 1024 * 1024;
    private const string HandshakeName = "tcp";

    private static IMemoryRental Memory => MemoryRental.Shared;

    private static ILogger Log => new Logger([]);

    [TearDown]
    public void TearDown()
    {
        GC.Collect();
    }

    [Test]
    public void SuccessfulHandshake_NotifiesBothHandlers()
    {
        using var pair = CreateConnectedPair(
            out var serverHandler,
            out var clientHandler);

        Assert.That(serverHandler.ConnectedCount, Is.EqualTo(1));
        Assert.That(clientHandler.ConnectedCount, Is.EqualTo(1));
        Assert.That(serverHandler.Endpoint, Is.Not.Null);
        Assert.That(clientHandler.Endpoint, Is.Not.Null);
        Assert.That(clientHandler.Endpoint!.IsConnected, Is.True);
        Assert.That(serverHandler.Endpoint!.IsConnected, Is.True);
    }

    [Test]
    public void VersionMismatch_StopsClientWithProtocolMismatchAndNoConnection()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        var handler = new RecordingClientHandler();
        RawReliableTcpClientEndpoint? client = null;
        Socket? acceptedSocket = null;
        try
        {
            client = CreateClientEndpoint(listener, handler);

            Assert.That(client.Start(), Is.True, "the client start request must be accepted");

            acceptedSocket = AcceptWithTimeout(listener, Timeout);

            UnionDataList request = ReadFrame(acceptedSocket);
            try
            {
                Assert.That(request.Elements[0].Alias.ByteValue,
                    Is.EqualTo(RawReliableFrameTypes.HandshakeRequest));
            }
            finally
            {
                request.Release();
            }

            SendHandshakeResponse(acceptedSocket,
                (short)(RawReliableHandshake.Version + 1), HandshakeName, MessageMaxByteSize);

            Assert.That(handler.Stopped.Wait(Timeout), Is.True,
                "the client must stop after a version mismatch");
            Assert.That(handler.ConnectedCount, Is.Zero,
                "a version mismatch must never notify the handler of a connection");
            Assert.That(handler.StoppedReason, Is.TypeOf<ProtocolMismatch>());
            Assert.That(((ProtocolMismatch)handler.StoppedReason!).Text, Is.Not.Empty,
                "the version-mismatch stop reason must carry diagnostic text");
        }
        finally
        {
            StopQuietly(client, handler.Stopped);
            DisposeQuietly(acceptedSocket);
            StopListener(listener);
        }
    }

    [Test]
    public void NameMismatch_StopsClientWithProtocolMismatchAndFactoryIsNotCalled()
    {
        var factory = new RecordingFactory(_ => new RecordingServerHandler());
        var serverHandler = new RecordingServerHandler();
        var clientHandler = new RecordingClientHandler();
        int port = GetFreeLoopbackPort();

        var server = CreateServer(port, "server-name", MessageMaxByteSize, factory);
        var client = new RawReliableTcpClientTransport(
            IPAddress.Loopback,
            port,
            TimeSpan.FromSeconds(30),
            MessageMaxByteSize,
            Log,
            Memory,
            bufferCapacityBytes: BufferCapacityBytes,
            handshakeName: "client-name");

        try
        {
            Assert.That(client.Init(clientHandler), Is.True);
            Assert.That(client.Start(_ => { }), Is.True);

            Assert.That(clientHandler.Stopped.Wait(Timeout), Is.True,
                "the client must stop after a name mismatch");
            Assert.That(clientHandler.ConnectedCount, Is.Zero);
            Assert.That(clientHandler.StoppedReason, Is.TypeOf<ProtocolMismatch>());
            var mismatch = (ProtocolMismatch)clientHandler.StoppedReason!;
            Assert.That(mismatch.Text, Does.Contain("name mismatch"));
            Assert.That(factory.Calls, Is.Zero,
                "a name mismatch must be rejected before the handler factory is invoked");
            Assert.That(serverHandler.ConnectedCount, Is.Zero);
        }
        finally
        {
            StopQuietly(client, clientHandler.Stopped);
            StopQuietly(server);
        }
    }

    [Test]
    public void FactoryReturnsNull_StopsClientWithHandshakeRejected()
    {
        var factory = new RecordingFactory(_ => null);
        var serverHandler = new RecordingServerHandler();
        var clientHandler = new RecordingClientHandler();
        int port = GetFreeLoopbackPort();

        var server = CreateServer(port, HandshakeName, MessageMaxByteSize, factory);
        var client = CreateClientTransport(port, clientHandler);

        try
        {
            Assert.That(client.Start(_ => { }), Is.True);

            Assert.That(clientHandler.Stopped.Wait(Timeout), Is.True,
                "the client must stop when the factory rejects the session");
            Assert.That(clientHandler.ConnectedCount, Is.Zero);
            Assert.That(clientHandler.StoppedReason, Is.TypeOf<HandshakeRejected>());
            Assert.That(factory.Calls, Is.EqualTo(1));
            Assert.That(serverHandler.ConnectedCount, Is.Zero);
        }
        finally
        {
            StopQuietly(client, clientHandler.Stopped);
            StopQuietly(server);
        }
    }

    [Test]
    public void HandshakeTimeout_WithoutServerResponse_StopsWithTimeOut()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        var handler = new RecordingClientHandler();
        RawReliableTcpClientEndpoint? client = null;
        Socket? acceptedSocket = null;
        try
        {
            client = CreateClientEndpoint(listener, handler, handshakeTimeout: TimeSpan.FromMilliseconds(300));

            Assert.That(client.Start(), Is.True, "the client start request must be accepted");

            acceptedSocket = AcceptWithTimeout(listener, Timeout);

            UnionDataList request = ReadFrame(acceptedSocket);
            request.Release();

            Assert.That(handler.Stopped.Wait(Timeout), Is.True,
                "the client must stop when the handshake deadline elapses");
            Assert.That(handler.ConnectedCount, Is.Zero);
            Assert.That(handler.StoppedReason, Is.TypeOf<TimeOut>());
        }
        finally
        {
            StopQuietly(client, handler.Stopped);
            DisposeQuietly(acceptedSocket);
            StopListener(listener);
        }
    }

    [Test]
    public void MaxSizeNegotiation_ChoosesMinimum()
    {
        using var pair = CreateConnectedPair(
            out var serverHandler,
            out var clientHandler,
            serverMessageMaxByteSize: 4096,
            clientMessageMaxByteSize: 100000);

        Assert.That(clientHandler.Endpoint!.MessageMaxByteSize, Is.EqualTo(4096),
            "the client must adopt the smaller of the two advertised maxima");
        Assert.That(serverHandler.Endpoint!.MessageMaxByteSize, Is.EqualTo(4096),
            "the server must adopt the smaller of the two advertised maxima");
    }

    [Test]
    public void SendAboveNegotiatedMaximum_IsRejectedOnBothSides()
    {
        using var pair = CreateConnectedPair(
            out var serverHandler,
            out var clientHandler,
            serverMessageMaxByteSize: 4096,
            clientMessageMaxByteSize: 100000);

        Assert.That(clientHandler.Endpoint!.MessageMaxByteSize, Is.EqualTo(4096));
        Assert.That(serverHandler.Endpoint!.MessageMaxByteSize, Is.EqualTo(4096));

        Assert.That(SendOversizedPayload(clientHandler.Endpoint), Is.EqualTo(SendResult.MessageTooBig),
            "the client must reject a payload above the negotiated maximum");
        Assert.That(SendOversizedPayload(serverHandler.Endpoint), Is.EqualTo(SendResult.MessageTooBig),
            "the server must reject a payload above the negotiated maximum");
    }

    [Test]
    public void RejectLinger_DefaultIsTwoSeconds()
    {
        var server = new RawReliableTcpServerTransport(
            IPAddress.Loopback,
            0,
            10,
            TimeSpan.FromSeconds(30),
            MessageMaxByteSize,
            Log,
            Memory);

        Assert.That(((RawReliableAdmissionServerTransport)server).RejectLinger,
            Is.EqualTo(TimeSpan.FromSeconds(2)));
    }

    [Test]
    public void RejectLinger_ConfiguredDelayBoundsServerCloseTime()
    {
        int port = GetFreeLoopbackPort();
        var factory = new RecordingFactory(_ => null);
        var server = CreateServer(port, HandshakeName, MessageMaxByteSize, factory,
            rejectLinger: TimeSpan.FromMilliseconds(100));

        Socket? clientSocket = null;
        try
        {
            clientSocket = ConnectRawClient(port);

            SendHandshakeRequest(clientSocket, HandshakeName, MessageMaxByteSize);
            var (status, _) = ReadHandshakeReject(clientSocket);
            Assert.That(status, Is.EqualTo(RawReliableHandshake.HandshakeStatus.Rejected));
            Assert.That(factory.Calls, Is.EqualTo(1));

            TimeSpan elapsed = WaitForPeerClose(clientSocket, Timeout);

            Assert.That(elapsed, Is.GreaterThanOrEqualTo(TimeSpan.FromMilliseconds(90)),
                "the server must honour the configured reject linger before closing");
            Assert.That(elapsed, Is.LessThan(TimeSpan.FromSeconds(1)),
                "the server must not linger indefinitely");
        }
        finally
        {
            DisposeQuietly(clientSocket);
            StopQuietly(server);
        }
    }

    [Test]
    public void OversizedHandshakeLengthPrefix_IsRejectedBeforeBodyAllocationAndClosesPeer()
    {
        int port = GetFreeLoopbackPort();
        var factory = new RecordingFactory(_ =>
            throw new AssertionException("the factory must not be invoked for an oversized prefix"));
        var server = CreateServer(port, HandshakeName, MessageMaxByteSize, factory, handshakeMaxByteSize: 128);

        Socket? clientSocket = null;
        try
        {
            clientSocket = ConnectRawClient(port);

            var length = new UnionDataMemoryAlias { IntValue = 129 };
            SendRaw(clientSocket, new[] { length.Byte0, length.Byte1, length.Byte2, length.Byte3 });

            TimeSpan elapsed = WaitForPeerClose(clientSocket, Timeout);

            Assert.That(elapsed, Is.LessThan(TimeSpan.FromSeconds(5)),
                "an oversized handshake prefix must close the peer promptly");
            Assert.That(factory.Calls, Is.Zero);
        }
        finally
        {
            DisposeQuietly(clientSocket);
            StopQuietly(server);
        }
    }

    [Test]
    public void PeerMessageMaxBelowHandshakeMax_IsRejectedWithCapacityExceeded()
    {
        int port = GetFreeLoopbackPort();
        var factory = new RecordingFactory(_ =>
            throw new AssertionException("the factory must not be invoked for a capacity rejection"));
        var server = CreateServer(port, HandshakeName, MessageMaxByteSize, factory, handshakeMaxByteSize: 128);

        Socket? clientSocket = null;
        try
        {
            clientSocket = ConnectRawClient(port);

            SendHandshakeRequest(clientSocket, HandshakeName, 64);

            var (status, text) = ReadHandshakeReject(clientSocket);

            Assert.That(status, Is.EqualTo(RawReliableHandshake.HandshakeStatus.CapacityExceeded));
            Assert.That(text, Does.Contain("capacity"));
            Assert.That(factory.Calls, Is.Zero);
        }
        finally
        {
            DisposeQuietly(clientSocket);
            StopQuietly(server);
        }
    }

    [Test]
    public void PendingResponseCannotBeSent_StopsSessionWithTextFailWithoutOnConnected()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        var handler = new RecordingServerHandler();
        RawReliableTcpServerEndpoint? endpoint = null;
        Socket? acceptedSocket = null;
        try
        {
            var rawClient = ConnectRawClient(((IPEndPoint)listener.LocalEndpoint).Port);
            try
            {
                acceptedSocket = AcceptWithTimeout(listener, Timeout);
                var remote = new IpEndPoint((IPEndPoint)acceptedSocket.RemoteEndPoint!);
                var response = RawReliableHandshake.BuildResponse(
                    RawReliableHandshake.Version, HandshakeName, 1);

                endpoint = new RawReliableTcpServerEndpoint(
                    "it-pending-response",
                    remote,
                    acceptedSocket,
                    handler,
                    response,
                    1,
                    BufferCapacityBytes,
                    Memory,
                    Log,
                    TimeSpan.FromSeconds(30),
                    RemoteHalfClosePolicy.GracefulSendShutdown,
                    16 * 1024,
                    16 * 1024,
                    TimeSpan.FromSeconds(2),
                    10);
                acceptedSocket = null;

                Assert.That(endpoint.Start(), Is.True);

                Assert.That(handler.Stopped.Wait(Timeout), Is.True,
                    "a response that cannot be sent must stop the session");
                Assert.That(handler.ConnectedCount, Is.Zero,
                    "the handler must not observe a connection for an unsendable response");
                Assert.That(handler.StoppedReason, Is.TypeOf<TextFail>());
            }
            finally
            {
                DisposeQuietly(rawClient);
            }
        }
        finally
        {
            StopQuietly(endpoint, handler.Stopped);
            DisposeQuietly(acceptedSocket);
            StopListener(listener);
        }
    }

    [Test]
    public void HandshakeFrames_AreNotDeliveredThroughOnReceived()
    {
        using var pair = CreateConnectedPair(
            out var serverHandler,
            out var clientHandler);

        Assert.That(serverHandler.ReceivedValues, Is.Empty,
            "the server handler must not receive the client handshake frame");
        Assert.That(clientHandler.ReceivedValues, Is.Empty,
            "the client handler must not receive the server handshake response");
    }

    #region Pair helpers

    private static int GetFreeLoopbackPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static RawReliableTcpServerTransport CreateServer(
        int port,
        string handshakeName,
        int messageMaxByteSize,
        RecordingFactory factory,
        TimeSpan? rejectLinger = null,
        int? handshakeMaxByteSize = null,
        TimeSpan? handshakeTimeout = null)
    {
        var server = new RawReliableTcpServerTransport(
            IPAddress.Loopback,
            port,
            10,
            TimeSpan.FromSeconds(30),
            messageMaxByteSize,
            Log,
            Memory,
            bufferCapacityBytes: BufferCapacityBytes,
            handshakeName: handshakeName,
            handshakeMaxByteSize: handshakeMaxByteSize,
            handshakeTimeout: handshakeTimeout,
            rejectLinger: rejectLinger);

        Assert.That(server.Init(factory), Is.True, "the server initialization must succeed");
        Assert.That(server.Start(_ => { }), Is.True, "the server start request must be accepted");
        return server;
    }

    private static RawReliableTcpClientTransport CreateClientTransport(
        int port,
        RecordingClientHandler handler,
        int messageMaxByteSize = MessageMaxByteSize,
        TimeSpan? handshakeTimeout = null)
    {
        var client = new RawReliableTcpClientTransport(
            IPAddress.Loopback,
            port,
            TimeSpan.FromSeconds(30),
            messageMaxByteSize,
            Log,
            Memory,
            bufferCapacityBytes: BufferCapacityBytes,
            handshakeName: HandshakeName,
            handshakeTimeout: handshakeTimeout);

        Assert.That(client.Init(handler), Is.True, "the client initialization must succeed");
        return client;
    }

    private static RawReliableTcpClientEndpoint CreateClientEndpoint(
        TcpListener listener,
        RecordingClientHandler handler,
        TimeSpan? handshakeTimeout = null)
    {
        return new RawReliableTcpClientEndpoint(
            "it-handshake-client",
            new IpEndPoint((IPEndPoint)listener.LocalEndpoint),
            handler,
            MessageMaxByteSize,
            BufferCapacityBytes,
            Memory,
            Log,
            pingOptions: null,
            connectTimeout: Timeout,
            gracefulShutdownDeadline: TimeSpan.FromSeconds(2),
            handshakeName: HandshakeName,
            handshakeTimeout: handshakeTimeout);
    }

    private static Pair CreateConnectedPair(
        out RecordingServerHandler serverHandler,
        out RecordingClientHandler clientHandler,
        int serverMessageMaxByteSize = MessageMaxByteSize,
        int clientMessageMaxByteSize = MessageMaxByteSize)
    {
        var createdServerHandler = new RecordingServerHandler();
        var createdClientHandler = new RecordingClientHandler();
        var factory = new RecordingFactory(_ => createdServerHandler);
        int port = GetFreeLoopbackPort();

        var server = CreateServer(port, HandshakeName, serverMessageMaxByteSize, factory);
        var client = CreateClientTransport(port, createdClientHandler, clientMessageMaxByteSize);

        try
        {
            Assert.That(client.Start(_ => { }), Is.True, "the client start request must be accepted");

            if (!createdServerHandler.Connected.Wait(Timeout))
            {
                throw new AssertionException("The server handler did not observe the logical connection");
            }

            if (!createdClientHandler.Connected.Wait(Timeout))
            {
                throw new AssertionException("The client handler did not observe the logical connection");
            }
        }
        catch
        {
            StopQuietly(client, createdClientHandler.Stopped);
            StopQuietly(server, createdServerHandler.Stopped);
            throw;
        }

        serverHandler = createdServerHandler;
        clientHandler = createdClientHandler;
        return new Pair(server, client, createdServerHandler, createdClientHandler);
    }

    private static SendResult SendOversizedPayload(IRawReliableEndpoint endpoint)
    {
        var message = Memory.CollectablePool.Acquire<UnionDataList>();
        for (int i = 0; i < 1000; i++)
        {
            message.PutLast(new UnionData(i));
        }

        return endpoint.Send(message);
    }

    private sealed class Pair : IDisposable
    {
        private readonly RawReliableTcpServerTransport _server;
        private readonly RawReliableTcpClientTransport _client;
        private readonly RecordingServerHandler _serverHandler;
        private readonly RecordingClientHandler _clientHandler;

        public Pair(RawReliableTcpServerTransport server, RawReliableTcpClientTransport client,
            RecordingServerHandler serverHandler, RecordingClientHandler clientHandler)
        {
            _server = server;
            _client = client;
            _serverHandler = serverHandler;
            _clientHandler = clientHandler;
        }

        public void Dispose()
        {
            StopQuietly(_client, _clientHandler.Stopped);
            StopQuietly(_server, _serverHandler.Stopped);
        }
    }

    #endregion

    #region Raw peer helpers

    private static Socket AcceptWithTimeout(TcpListener listener, TimeSpan timeout)
    {
        IAsyncResult pending = listener.BeginAcceptSocket(null, null);
        using var wait = pending.AsyncWaitHandle;
        if (!wait.WaitOne(timeout))
        {
            throw new AssertionException("Timed out waiting for a loopback client connection");
        }

        var socket = listener.EndAcceptSocket(pending);
        socket.ReceiveTimeout = (int)Timeout.TotalMilliseconds;
        return socket;
    }

    private static Socket ConnectRawClient(int port)
    {
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
        {
            NoDelay = true,
            ReceiveTimeout = (int)Timeout.TotalMilliseconds
        };

        try
        {
            socket.Connect(IPAddress.Loopback, port);
            return socket;
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    private static void SendHandshakeRequest(Socket socket, string name, int messageMaxByteSize)
    {
        var request = RawReliableHandshake.BuildRequest(name, messageMaxByteSize);
        request.PutFirst(RawReliableFrameTypes.HandshakeRequest);
        SendModel(socket, request);
    }

    private static void SendHandshakeResponse(Socket socket, short version, string name, int messageMaxByteSize)
    {
        var response = RawReliableHandshake.BuildResponse(version, name, messageMaxByteSize);
        response.PutFirst(RawReliableFrameTypes.HandshakeResponse);
        SendModel(socket, response);
    }

    private static void SendModel(Socket socket, UnionDataList model)
    {
        IMultiRefByteArray? frame = null;
        try
        {
            frame = UnionDataListCompositor.Encode(model, Memory.ByteArraysPool);
            Assert.That(frame, Is.Not.Null, "the raw peer frame must be encodable");
            SendAll(socket, frame!);
        }
        finally
        {
            frame?.Release();
            model.Release();
        }
    }

    private static void SendRaw(Socket socket, byte[] bytes)
    {
        int offset = 0;
        while (offset < bytes.Length)
        {
            int sent = socket.Send(bytes, offset, bytes.Length - offset, SocketFlags.None);
            Assert.That(sent, Is.GreaterThan(0));
            offset += sent;
        }
    }

    private static void SendAll(Socket socket, IMultiRefByteArray buffer)
    {
        int offset = buffer.Offset;
        int remaining = buffer.Count;
        while (remaining > 0)
        {
            int sent = socket.Send(buffer.Array, offset, remaining, SocketFlags.None);
            Assert.That(sent, Is.GreaterThan(0));
            offset += sent;
            remaining -= sent;
        }
    }

    private static UnionDataList ReadFrame(Socket socket)
    {
        var header = new byte[4];
        ReadExactly(socket, header, header.Length);

        int length = BitConverter.ToInt32(header, 0);
        Assert.That(length, Is.GreaterThan(0), "the frame must declare a positive body length");

        var body = new byte[length];
        ReadExactly(socket, body, body.Length);

        IMultiRefByteArray? buffer = null;
        UnionDataList? message = null;
        try
        {
            buffer = Memory.ByteArraysPool.Acquire(length);
            Buffer.BlockCopy(body, 0, buffer.Array, buffer.Offset, length);

            var source = new ByteSourceFromArray(buffer);
            message = Memory.CollectablePool.Acquire<UnionDataList>();
            if (!message.Deserialize(ref source, Memory.ByteArraysPool))
            {
                throw new AssertionException("the raw peer failed to decode the UnionDataList frame");
            }

            var result = message;
            message = null;
            return result;
        }
        finally
        {
            message?.Release();
            buffer?.Release();
        }
    }

    private static void ReadExactly(Socket socket, byte[] buffer, int count)
    {
        int offset = 0;
        while (offset < count)
        {
            int received = socket.Receive(buffer, offset, count - offset, SocketFlags.None);
            if (received == 0)
            {
                throw new AssertionException(
                    "the raw peer observed the connection closing before a complete frame arrived");
            }

            offset += received;
        }
    }

    private static (RawReliableHandshake.HandshakeStatus Status, string? Text) ReadHandshakeReject(
        Socket socket)
    {
        UnionDataList frame = ReadFrame(socket);
        try
        {
            Assert.That(frame.Elements.Count, Is.GreaterThan(0));
            Assert.That(frame.Elements[0].Alias.ByteValue,
                Is.EqualTo(RawReliableFrameTypes.HandshakeReject));
            frame.PopFirst();

            if (!RawReliableHandshake.TryParseReject(frame, out _, out _, out var status, out string? text))
            {
                throw new AssertionException("the raw peer failed to parse the handshake reject");
            }

            return (status, text);
        }
        finally
        {
            frame.Release();
        }
    }

    private static TimeSpan WaitForPeerClose(Socket socket, TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();
        var probe = new byte[1];
        try
        {
            int received = socket.Receive(probe);
            Assert.That(received, Is.EqualTo(0), "the peer must close the connection");
        }
        catch (SocketException ex)
        {
            Assert.That(ex.SocketErrorCode, Is.Not.EqualTo(SocketError.TimedOut),
                "the peer must close the connection instead of leaving it open");
        }
        catch (ObjectDisposedException)
        {
            // The peer is already gone.
        }

        stopwatch.Stop();
        Assert.That(stopwatch.Elapsed, Is.LessThan(timeout), "the peer must close before the test deadline");
        return stopwatch.Elapsed;
    }

    private static void StopQuietly(ITransport? transport)
    {
        if (transport == null)
        {
            return;
        }

        try
        {
            transport.Stop();
        }
        catch (Exception)
        {
            // best-effort cleanup
        }
    }

    private static void StopQuietly(ITransport? transport, ManualResetEventSlim stopped)
    {
        if (transport == null)
        {
            return;
        }

        try
        {
            transport.Stop();
        }
        catch (Exception)
        {
            // best-effort cleanup
        }

        stopped.Wait(TimeSpan.FromSeconds(5));
    }

    private static void StopQuietly(IRawReliableEndpoint? endpoint, ManualResetEventSlim stopped)
    {
        if (endpoint == null)
        {
            return;
        }

        try
        {
            endpoint.Disconnect(new UserIntention("integration-test-cleanup"));
        }
        catch (Exception)
        {
            // best-effort cleanup
        }

        stopped.Wait(TimeSpan.FromSeconds(5));
    }

    private static void DisposeQuietly(Socket? socket)
    {
        if (socket == null)
        {
            return;
        }

        try
        {
            socket.Dispose();
        }
        catch (Exception)
        {
            // best-effort cleanup
        }
    }

    private static void StopListener(TcpListener listener)
    {
        try
        {
            listener.Stop();
        }
        catch (Exception)
        {
            // best-effort cleanup
        }
    }

    #endregion

    #region Recording doubles

    private sealed class RecordingFactory : IRawReliableServerHandlerFactory<IRawReliableServerHandler>
    {
        private readonly Func<IEndPoint, IRawReliableServerHandler?> _create;
        private int _calls;

        public RecordingFactory(Func<IEndPoint, IRawReliableServerHandler?> create)
        {
            _create = create;
        }

        public int Calls => Volatile.Read(ref _calls);

        public IRawReliableServerHandler? TryCreate(IEndPoint remote)
        {
            Interlocked.Increment(ref _calls);
            return _create(remote);
        }
    }

    private sealed class RecordingServerHandler : IRawReliableServerHandler
    {
        private readonly object _lock = new();
        private readonly List<int> _receivedValues = new();

        public readonly ManualResetEventSlim Connected = new(false);
        public readonly ManualResetEventSlim Stopped = new(false);

        public int ConnectedCount { get; private set; }

        public int DisconnectedCount { get; private set; }

        public int StoppedCount { get; private set; }

        public IRawReliableEndpoint? Endpoint { get; private set; }

        public StopReason? StoppedReason { get; private set; }

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
            }

            Connected.Set();
        }

        public void OnReceived(UnionDataList receivedBuffer)
        {
            try
            {
                lock (_lock)
                {
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
            }
        }

        public void OnStopped(StopReason reason)
        {
            lock (_lock)
            {
                StoppedCount++;
                StoppedReason = reason;
            }

            Stopped.Set();
        }
    }

    private sealed class RecordingClientHandler : IRawReliableClientHandler
    {
        private readonly object _lock = new();
        private readonly List<int> _receivedValues = new();

        public readonly ManualResetEventSlim Connected = new(false);
        public readonly ManualResetEventSlim Stopped = new(false);

        public int ConnectedCount { get; private set; }

        public int DisconnectedCount { get; private set; }

        public int StoppedCount { get; private set; }

        public IRawReliableEndpoint? Endpoint { get; private set; }

        public StopReason? StoppedReason { get; private set; }

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
            }

            Connected.Set();
        }

        public void OnReceived(UnionDataList receivedBuffer)
        {
            try
            {
                lock (_lock)
                {
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
            }
        }

        public void OnStopped(StopReason reason)
        {
            lock (_lock)
            {
                StoppedCount++;
                StoppedReason = reason;
            }

            Stopped.Set();
        }
    }

    #endregion
}
