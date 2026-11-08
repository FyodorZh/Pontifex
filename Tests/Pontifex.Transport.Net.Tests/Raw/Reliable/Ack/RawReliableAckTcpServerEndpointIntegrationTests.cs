using System.Net;
using System.Net.Sockets;
using Actuarius.Memory;
using Pontifex.NetSockets;
using Pontifex.Raw.Reliable;
using Pontifex.Raw.Reliable.Ack;
using Pontifex.StopReasons;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Tests.Raw.Reliable.Ack;

/// <summary>
/// Real loopback TCP integration tests for <see cref="RawReliableAckTcpServerEndpoint"/> together with the
/// committed real <see cref="RawReliableAckTcpClientEndpoint"/>, plus raw-socket probes for the rejection,
/// oversize-prefix and deadline paths.
/// </summary>
[Category("Integration")]
public sealed class RawReliableAckTcpServerEndpointIntegrationTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private const int MessageMaxByteSize = 1024 * 1024;
    private const long BufferCapacityBytes = 1024 * 1024;
    private const byte AckRequestFrame = 5; // RawReliableEndpoint.MessageType.AckRequestMessage

    private static IMemoryRental Memory => MemoryRental.Shared;

    private static ILogger Log => new Logger([]);

    [TearDown]
    public void TearDown()
    {
        GC.Collect();
    }

    [Test]
    public void HappyPath_RealClientAndServerEndpoints_CompleteAckHandshake()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var remoteEndPoint = new IpEndPoint((IPEndPoint)listener.LocalEndpoint);

        var clientHandler = new RecordingAckClientHandler();
        var serverHandler = new RecordingAckServerHandler();

        RawReliableAckTcpClientEndpoint? client = null;
        RawReliableAckTcpServerEndpoint? server = null;
        Socket? acceptedSocket = null;
        try
        {
            client = new RawReliableAckTcpClientEndpoint(
                "it-client",
                remoteEndPoint,
                clientHandler,
                MessageMaxByteSize,
                BufferCapacityBytes,
                Memory,
                Log,
                pingOptions: null,
                connectTimeout: Timeout);

            Assert.That(client.Start(), Is.True, "the client start request must be accepted");

            acceptedSocket = AcceptWithTimeout(listener, Timeout);

            var captured = new AcceptCapture();
            using var acceptDone = new ManualResetEventSlim(false);
            IEndPoint? factoryEndPoint = null;
            int factoryAckCount = -1;
            int factoryAckFirst = -1;

            RawReliableAckTcpServerEndpoint.BeginAccept(
                "it-server",
                acceptedSocket,
                (endPoint, ackData) =>
                {
                    Volatile.Write(ref factoryEndPoint, endPoint);
                    Volatile.Write(ref factoryAckCount, ackData.Elements.Count);
                    Volatile.Write(
                        ref factoryAckFirst,
                        ackData.Elements.Count > 0 ? ackData.Elements[0].Alias.ByteValue : -1);
                    return serverHandler;
                },
                MessageMaxByteSize,
                BufferCapacityBytes,
                Memory,
                Log,
                TimeSpan.FromSeconds(30),
                result =>
                {
                    captured.Value = result;
                    acceptDone.Set();
                },
                ackHandshakeTimeout: Timeout);

            // The handshake owns the accepted socket from this point on.
            acceptedSocket = null;

            Assert.That(acceptDone.Wait(Timeout), Is.True, "the accept callback must complete");
            Assert.That(captured.Value, Is.Not.Null, "the accept callback must produce a result");

            AckAcceptResult acceptResult = captured.Value!.Value;
            Assert.That(acceptResult.Status, Is.EqualTo(AckAcceptStatus.Accepted));
            Assert.That(acceptResult.Endpoint, Is.Not.Null);
            Assert.That(acceptResult.Error, Is.Null);
            server = acceptResult.Endpoint!;

            Assert.That(server.Start(), Is.True, "the accepted endpoint start request must be accepted");

            Assert.That(serverHandler.FillAckResponseCalled.Wait(Timeout), Is.True,
                "the server handler must fill the ack response");

            Assert.That(serverHandler.Connected.Wait(Timeout), Is.True,
                "the server handler must observe the logical connection");
            Assert.That(clientHandler.Connected.Wait(Timeout), Is.True,
                "the client handler must observe the logical connection");

            Assert.That(Volatile.Read(ref factoryAckCount), Is.EqualTo(1),
                "the server factory must receive the client ack payload without the frame type");
            Assert.That(Volatile.Read(ref factoryAckFirst), Is.EqualTo(0xAB),
                "the server factory must receive the client ack data");
            Assert.That(Volatile.Read(ref factoryEndPoint), Is.Not.Null,
                "the server factory must receive the remote endpoint");

            Assert.That(clientHandler.AckResponsePayload, Is.EqualTo(new[] { 0xCD }),
                "the client handler must receive the server ack response payload");
            Assert.That(serverHandler.ConnectedEndPoint, Is.SameAs(server),
                "the server handler must be given the accepted endpoint");
        }
        finally
        {
            StopQuietly(client, clientHandler.Stopped);
            StopQuietly(server, serverHandler.Stopped);
            DisposeQuietly(acceptedSocket);
            StopListener(listener);
        }
    }

    [Test]
    public void Rejection_FactoryReturnsNull_ReportsRejectedAndClosesPeer()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        Socket? clientSocket = null;
        Socket? acceptedSocket = null;
        try
        {
            clientSocket = ConnectRawClient(listener);
            acceptedSocket = listener.AcceptSocket();
            SendAckRequest(clientSocket, 0xAB);

            var captured = new AcceptCapture();
            using var acceptDone = new ManualResetEventSlim(false);

            RawReliableAckTcpServerEndpoint.BeginAccept(
                "it-reject",
                acceptedSocket,
                (_, _) => null,
                MessageMaxByteSize,
                BufferCapacityBytes,
                Memory,
                Log,
                TimeSpan.FromSeconds(30),
                result =>
                {
                    captured.Value = result;
                    acceptDone.Set();
                },
                ackHandshakeTimeout: Timeout);

            acceptedSocket = null;

            Assert.That(acceptDone.Wait(Timeout), Is.True, "the accept callback must complete");
            Assert.That(captured.Value, Is.Not.Null, "the accept callback must produce a result");

            AckAcceptResult acceptResult = captured.Value!.Value;
            Assert.That(acceptResult.Status, Is.EqualTo(AckAcceptStatus.Rejected));
            Assert.That(acceptResult.Endpoint, Is.Null);
            Assert.That(acceptResult.Reason, Is.TypeOf<AckRejected>());

            AssertPeerClosed(clientSocket);
        }
        finally
        {
            DisposeQuietly(clientSocket);
            DisposeQuietly(acceptedSocket);
            StopListener(listener);
        }
    }

    [Test]
    public void OversizePrefix_ReportsMessageTooBigBeforeBodyAllocationAndClosesPeer()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        Socket? clientSocket = null;
        Socket? acceptedSocket = null;
        try
        {
            clientSocket = ConnectRawClient(listener);
            acceptedSocket = listener.AcceptSocket();

            var length = new UnionDataMemoryAlias { IntValue = RawReliableAckInfo.AckMaxByteSize + 1 };
            SendRaw(clientSocket, new[] { length.Byte0, length.Byte1, length.Byte2, length.Byte3 });

            var captured = new AcceptCapture();
            using var acceptDone = new ManualResetEventSlim(false);

            RawReliableAckTcpServerEndpoint.BeginAccept(
                "it-too-big",
                acceptedSocket,
                (_, _) =>
                {
                    throw new AssertionException(
                        "the handler factory must not be invoked for an oversized prefix");
                },
                MessageMaxByteSize,
                BufferCapacityBytes,
                Memory,
                Log,
                TimeSpan.FromSeconds(30),
                result =>
                {
                    captured.Value = result;
                    acceptDone.Set();
                },
                ackHandshakeTimeout: Timeout);

            acceptedSocket = null;

            Assert.That(acceptDone.Wait(Timeout), Is.True, "the accept callback must complete");
            Assert.That(captured.Value, Is.Not.Null, "the accept callback must produce a result");

            AckAcceptResult acceptResult = captured.Value!.Value;
            Assert.That(acceptResult.Status, Is.EqualTo(AckAcceptStatus.MessageTooBig));
            Assert.That(acceptResult.Endpoint, Is.Null);

            AssertPeerClosed(clientSocket);
        }
        finally
        {
            DisposeQuietly(clientSocket);
            DisposeQuietly(acceptedSocket);
            StopListener(listener);
        }
    }

    [Test]
    public void HandshakeDeadline_NoData_ReportsTimeoutAndClosesPeer()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        Socket? clientSocket = null;
        Socket? acceptedSocket = null;
        try
        {
            clientSocket = ConnectRawClient(listener);
            acceptedSocket = listener.AcceptSocket();

            var captured = new AcceptCapture();
            using var acceptDone = new ManualResetEventSlim(false);
            var handshakeTimeout = TimeSpan.FromMilliseconds(200);

            RawReliableAckTcpServerEndpoint.BeginAccept(
                "it-timeout",
                acceptedSocket,
                (_, _) =>
                {
                    throw new AssertionException(
                        "the handler factory must not be invoked after a handshake timeout");
                },
                MessageMaxByteSize,
                BufferCapacityBytes,
                Memory,
                Log,
                TimeSpan.FromSeconds(30),
                result =>
                {
                    captured.Value = result;
                    acceptDone.Set();
                },
                ackHandshakeTimeout: handshakeTimeout);

            acceptedSocket = null;

            Assert.That(acceptDone.Wait(Timeout), Is.True, "the accept callback must complete");
            Assert.That(captured.Value, Is.Not.Null, "the accept callback must produce a result");

            AckAcceptResult acceptResult = captured.Value!.Value;
            Assert.That(acceptResult.Status, Is.EqualTo(AckAcceptStatus.Timeout));
            Assert.That(acceptResult.Endpoint, Is.Null);

            AssertPeerClosed(clientSocket);
        }
        finally
        {
            DisposeQuietly(clientSocket);
            DisposeQuietly(acceptedSocket);
            StopListener(listener);
        }
    }

    #region Helpers

    private static Socket AcceptWithTimeout(TcpListener listener, TimeSpan timeout)
    {
        IAsyncResult pending = listener.BeginAcceptSocket(null, null);
        using var wait = pending.AsyncWaitHandle;
        if (!wait.WaitOne(timeout))
        {
            throw new AssertionException("Timed out waiting for a loopback client connection");
        }

        return listener.EndAcceptSocket(pending);
    }

    private static Socket ConnectRawClient(TcpListener listener)
    {
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
        {
            NoDelay = true,
            ReceiveTimeout = (int)Timeout.TotalMilliseconds
        };

        try
        {
            socket.Connect(listener.LocalEndpoint);
            return socket;
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    private static void SendAckRequest(Socket socket, byte payload)
    {
        UnionDataList? ackData = null;
        IMultiRefByteArray? frame = null;
        try
        {
            ackData = Memory.CollectablePool.Acquire<UnionDataList>();
            ackData.PutLast(new UnionData(payload));
            ackData.PutFirst(AckRequestFrame);

            frame = UnionDataListCompositor.Encode(ackData, Memory.ByteArraysPool);
            Assert.That(frame, Is.Not.Null, "the ack request must be encodable");

            SendAll(socket, frame!);
        }
        finally
        {
            frame?.Release();
            ackData?.Release();
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

    private static void AssertPeerClosed(Socket socket)
    {
        // The timeout is armed on the raw client socket when it is created; setting it here can throw
        // "Invalid argument" once an RST has already been observed.
        var probe = new byte[1];
        try
        {
            int received = socket.Receive(probe);
            Assert.That(received, Is.EqualTo(0),
                "the server must close the connection without sending a frame");
        }
        catch (SocketException ex)
        {
            Assert.That(ex.SocketErrorCode, Is.Not.EqualTo(SocketError.TimedOut),
                "the server must close the connection instead of leaving it open");
        }
        catch (ObjectDisposedException)
        {
            // The peer is already gone.
        }
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

    #region Recording handlers

    private sealed class AcceptCapture
    {
        public AckAcceptResult? Value;
    }

    private sealed class RecordingAckClientHandler : IRawReliableAckClientHandler
    {
        public readonly ManualResetEventSlim Connected = new(false);
        public readonly ManualResetEventSlim Stopped = new(false);

        public byte[]? AckResponsePayload { get; private set; }

        public void FillAckData(UnionDataList ackData)
        {
            ackData.PutLast(new UnionData(0xAB));
        }

        public void OnConnected(IRawReliableEndpoint endPoint, UnionDataList ackResponse)
        {
            AckResponsePayload = ackResponse.Elements.Count > 0
                ? new[] { ackResponse.Elements[0].Alias.ByteValue }
                : Array.Empty<byte>();

            ackResponse.Release();
            Connected.Set();
        }

        public void OnReceived(UnionDataList receivedBuffer)
        {
            receivedBuffer.Release();
        }

        public void OnDisconnected(StopReason reason)
        {
        }

        public void OnStopped(StopReason reason)
        {
            Stopped.Set();
        }
    }

    private sealed class RecordingAckServerHandler : IRawReliableAckServerHandler
    {
        public readonly ManualResetEventSlim Connected = new(false);
        public readonly ManualResetEventSlim FillAckResponseCalled = new(false);
        public readonly ManualResetEventSlim Stopped = new(false);

        public IRawReliableEndpoint? ConnectedEndPoint { get; private set; }

        public void FillAckResponse(UnionDataList ackData)
        {
            ackData.PutLast(new UnionData(0xCD));
            FillAckResponseCalled.Set();
        }

        public void OnConnected(IRawReliableEndpoint endPoint)
        {
            ConnectedEndPoint = endPoint;
            Connected.Set();
        }

        public void OnReceived(UnionDataList receivedBuffer)
        {
            receivedBuffer.Release();
        }

        public void OnDisconnected(StopReason reason)
        {
        }

        public void OnStopped(StopReason reason)
        {
            Stopped.Set();
        }
    }

    #endregion
}
