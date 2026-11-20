using System.Net;
using System.Net.Sockets;
using Actuarius.Memory;
using Pontifex.NetSockets;
using Pontifex.Raw.Reliable;
using Pontifex.Raw.Reliable.Tcp;
using Pontifex.StopReasons;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Tests.Raw.Reliable.RawReliable;

/// <summary>
/// Real loopback TCP integration tests for <see cref="RawReliableTcpClientEndpoint"/>. The peer is a raw
/// socket, so the reliable client is verified without the reliable server carrier.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><description><b>No handshake</b>: the client handler must observe <c>OnConnected</c> as soon as the
/// socket connects, and the first decoded frame must be a payload or control frame sent by the endpoint, not an
/// acknowledgement request.</description></item>
/// <item><description><b>Coverage</b>: connect, send, receive, graceful disconnect, remote disconnect, remote
/// close, graceful stop, and the not-connected send rejection are covered here. The reliable server
/// carrier is covered separately in Stage 9.</description></item>
/// </list>
/// </remarks>
[Category("Integration")]
public sealed class RawReliableTcpClientEndpointIntegrationTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private const int MessageMaxByteSize = 1024 * 1024;
    private const long BufferCapacityBytes = 1024 * 1024;

    private const byte PayloadFrame = 1; // RawReliableEndpoint.MessageType.PayloadMessage
    private const byte DisconnectFrame = 2; // RawReliableEndpoint.MessageType.DisconnectMessage

    private static IMemoryRental Memory => MemoryRental.Shared;

    private static ILogger Log => new Logger([]);

    [TearDown]
    public void TearDown()
    {
        GC.Collect();
    }

    [Test]
    public void HappyPath_ConnectsAndNotifiesClientHandler()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        var handler = new RecordingRawReliableClientHandler();
        RawReliableTcpClientEndpoint? client = null;
        Socket? acceptedSocket = null;
        try
        {
            client = CreateEndpoint(listener, handler);

            Assert.That(client.Start(), Is.True, "the client start request must be accepted");

            acceptedSocket = AcceptWithTimeout(listener, Timeout);

            Assert.That(handler.Connected.Wait(Timeout), Is.True,
                "the client handler must observe the logical connection");
            Assert.That(handler.ConnectedCount, Is.EqualTo(1));
            Assert.That(handler.Endpoint, Is.SameAs(client),
                "the client handler must be given the connecting endpoint");
            Assert.That(client.IsConnected, Is.True,
                "the endpoint must be connected without any acknowledgement step");
        }
        finally
        {
            StopQuietly(client, handler.Stopped);
            DisposeQuietly(acceptedSocket);
            StopListener(listener);
        }
    }

    [Test]
    public void Send_TransmitsPayloadFrameToRawPeer()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        var handler = new RecordingRawReliableClientHandler();
        RawReliableTcpClientEndpoint? client = null;
        Socket? acceptedSocket = null;
        try
        {
            client = ConnectEndpoint(listener, handler, out acceptedSocket);

            var message = Memory.CollectablePool.Acquire<UnionDataList>();
            message.PutLast(new UnionData(42));

            Assert.That(client.Send(message), Is.EqualTo(SendResult.Ok),
                "a connected endpoint must admit a payload message");

            UnionDataList frame = ReadFrame(acceptedSocket!);
            try
            {
                Assert.That(frame.Elements.Count, Is.EqualTo(2));
                Assert.That(frame.Elements[0].Alias.ByteValue, Is.EqualTo(PayloadFrame),
                    "the first decoded value must be the payload frame type");
                Assert.That(frame.Elements[1].Alias.IntValue, Is.EqualTo(42),
                    "the decoded payload must match the sent value");
            }
            finally
            {
                frame.Release();
            }
        }
        finally
        {
            StopQuietly(client, handler.Stopped);
            DisposeQuietly(acceptedSocket);
            StopListener(listener);
        }
    }

    [Test]
    public void RawPeerPayload_IsDeliveredToHandler()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        var handler = new RecordingRawReliableClientHandler();
        RawReliableTcpClientEndpoint? client = null;
        Socket? acceptedSocket = null;
        try
        {
            client = ConnectEndpoint(listener, handler, out acceptedSocket);

            SendFrame(acceptedSocket!, new UnionData(PayloadFrame), new UnionData(7));

            Assert.That(handler.ReceivedCount.Wait(Timeout), Is.True,
                "the client handler must receive the raw peer payload");
            Assert.That(handler.ReceivedValues, Is.EqualTo(new[] { 7 }));
        }
        finally
        {
            StopQuietly(client, handler.Stopped);
            DisposeQuietly(acceptedSocket);
            StopListener(listener);
        }
    }

    [Test]
    public void RawPeerDisconnect_NotifiesDisconnectedThenStopped()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        var handler = new RecordingRawReliableClientHandler();
        RawReliableTcpClientEndpoint? client = null;
        Socket? acceptedSocket = null;
        try
        {
            client = ConnectEndpoint(listener, handler, out acceptedSocket);

            SendFrame(acceptedSocket!, new UnionData(DisconnectFrame));

            Assert.That(handler.Stopped.Wait(Timeout), Is.True,
                "the client handler must observe the stop after a remote disconnect");
            Assert.That(handler.DisconnectedCount, Is.EqualTo(1));
            Assert.That(handler.StoppedCount, Is.EqualTo(1));
            Assert.That(handler.Events,
                Is.EqualTo(new[] { "connected", "disconnected", "stopped" }),
                "the client handler must observe the documented lifecycle order");
        }
        finally
        {
            StopQuietly(client, handler.Stopped);
            DisposeQuietly(acceptedSocket);
            StopListener(listener);
        }
    }

    [Test]
    public void RawPeerClose_NotifiesDisconnectedThenStopped()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        var handler = new RecordingRawReliableClientHandler();
        RawReliableTcpClientEndpoint? client = null;
        Socket? acceptedSocket = null;
        try
        {
            client = ConnectEndpoint(listener, handler, out acceptedSocket);

            acceptedSocket!.Dispose();

            Assert.That(handler.Stopped.Wait(Timeout), Is.True,
                "the client handler must observe the stop after the peer closes");
            Assert.That(handler.DisconnectedCount, Is.EqualTo(1));
            Assert.That(handler.StoppedCount, Is.EqualTo(1));
            Assert.That(handler.Events,
                Is.EqualTo(new[] { "connected", "disconnected", "stopped" }),
                "the client handler must observe the documented lifecycle order");
        }
        finally
        {
            StopQuietly(client, handler.Stopped);
            DisposeQuietly(acceptedSocket);
            StopListener(listener);
        }
    }

    [Test]
    public void GracefulStop_SendsDisconnectFrame()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        var handler = new RecordingRawReliableClientHandler();
        RawReliableTcpClientEndpoint? client = null;
        Socket? acceptedSocket = null;
        try
        {
            client = ConnectEndpoint(listener, handler, out acceptedSocket);

            var reason = new UserIntention("integration-test", "client graceful stop");
            Assert.That(((IRawReliableEndpoint)client).Disconnect(reason), Is.True,
                "the disconnect request must be accepted");

            UnionDataList frame = ReadFrame(acceptedSocket!);
            try
            {
                Assert.That(frame.Elements.Count, Is.GreaterThan(0));
                Assert.That(frame.Elements[0].Alias.ByteValue, Is.EqualTo(DisconnectFrame),
                    "a graceful stop must send a disconnect frame");
            }
            finally
            {
                frame.Release();
            }

            Assert.That(handler.Stopped.Wait(Timeout), Is.True,
                "the client handler must observe the stop");
            Assert.That(handler.DisconnectedCount, Is.EqualTo(1));
            Assert.That(handler.StoppedCount, Is.EqualTo(1));
        }
        finally
        {
            StopQuietly(client, handler.Stopped);
            DisposeQuietly(acceptedSocket);
            StopListener(listener);
        }
    }

    [Test]
    public void Send_WhenNotConnected_ReturnsNotConnected()
    {
        var endpoint = new RawReliableTcpClientEndpoint(
            "it-noack-send-not-connected",
            new IpEndPoint(new IPEndPoint(IPAddress.Loopback, 1)),
            new RecordingRawReliableClientHandler(),
            MessageMaxByteSize,
            BufferCapacityBytes,
            Memory,
            Log,
            pingOptions: null);

        var message = Memory.CollectablePool.Acquire<UnionDataList>();
        message.PutLast(new UnionData(1));

        Assert.That(endpoint.Send(message), Is.EqualTo(SendResult.NotConnected),
            "a constructed endpoint must reject a send without consuming the connection");
    }

    [Test]
    public void HandlerDisconnectsInOnConnected_StopsCleanly()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        var handler = new RecordingRawReliableClientHandler { DisconnectOnConnected = true };
        RawReliableTcpClientEndpoint? client = null;
        Socket? acceptedSocket = null;
        try
        {
            client = CreateEndpoint(listener, handler);

            Assert.That(client.Start(), Is.True, "the client start request must be accepted");

            acceptedSocket = AcceptWithTimeout(listener, Timeout);

            // Read the graceful disconnect frame before waiting for the terminal notification: the endpoint
            // keeps the connection open until its graceful shutdown deadline expires.
            UnionDataList frame = ReadFrame(acceptedSocket);
            try
            {
                Assert.That(frame.Elements.Count, Is.GreaterThan(0));
                Assert.That(frame.Elements[0].Alias.ByteValue, Is.EqualTo(DisconnectFrame),
                    "the graceful shutdown started from OnConnected must send a disconnect frame");
            }
            finally
            {
                frame.Release();
            }

            Assert.That(handler.Stopped.Wait(Timeout), Is.True,
                "a handler-initiated disconnect must stop the endpoint");
            Assert.That(handler.ConnectedCount, Is.EqualTo(1));
            Assert.That(handler.DisconnectedCount, Is.EqualTo(1));
            Assert.That(handler.StoppedCount, Is.EqualTo(1));
            Assert.That(handler.StoppedReason, Is.TypeOf<UserIntention>(),
                "the handler-initiated disconnect must not become a receiver-start failure");
            Assert.That(client.IsConnected, Is.False);
        }
        finally
        {
            StopQuietly(client, handler.Stopped);
            DisposeQuietly(acceptedSocket);
            StopListener(listener);
        }
    }

    #region Helpers

    private static RawReliableTcpClientEndpoint CreateEndpoint(
        TcpListener listener,
        RecordingRawReliableClientHandler handler)
    {
        return new RawReliableTcpClientEndpoint(
            "it-noack-client",
            new IpEndPoint((IPEndPoint)listener.LocalEndpoint),
            handler,
            MessageMaxByteSize,
            BufferCapacityBytes,
            Memory,
            Log,
            pingOptions: null,
            connectTimeout: Timeout,
            gracefulShutdownDeadline: TimeSpan.FromSeconds(2));
    }

    private static RawReliableTcpClientEndpoint ConnectEndpoint(
        TcpListener listener,
        RecordingRawReliableClientHandler handler,
        out Socket? acceptedSocket)
    {
        var client = CreateEndpoint(listener, handler);
        Assert.That(client.Start(), Is.True, "the client start request must be accepted");

        acceptedSocket = AcceptWithTimeout(listener, Timeout);
        Assert.That(handler.Connected.Wait(Timeout), Is.True,
            "the client handler must observe the logical connection");

        return client;
    }

    private static void PreparePeerSocket(Socket socket)
    {
        // Arm the receive timeout before any reset can be observed; setting it after an RST can throw.
        socket.ReceiveTimeout = (int)Timeout.TotalMilliseconds;
    }

    private static Socket AcceptWithTimeout(TcpListener listener, TimeSpan timeout)
    {
        IAsyncResult pending = listener.BeginAcceptSocket(null, null);
        using var wait = pending.AsyncWaitHandle;
        if (!wait.WaitOne(timeout))
        {
            throw new AssertionException("Timed out waiting for a loopback client connection");
        }

        var socket = listener.EndAcceptSocket(pending);
        PreparePeerSocket(socket);
        return socket;
    }

    private static void SendFrame(Socket socket, params UnionData[] elements)
    {
        var message = Memory.CollectablePool.Acquire<UnionDataList>();
        IMultiRefByteArray? frame = null;
        try
        {
            foreach (var element in elements)
            {
                message.PutLast(element);
            }

            frame = UnionDataListCompositor.Encode(message, Memory.ByteArraysPool);
            Assert.That(frame, Is.Not.Null, "the raw peer frame must be encodable");

            SendAll(socket, frame!);
        }
        finally
        {
            frame?.Release();
            message.Release();
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

    #region Recording handler

    private sealed class RecordingRawReliableClientHandler : IRawReliableClientHandler
    {
        private readonly object _lock = new();
        private readonly List<int> _receivedValues = new();
        private readonly List<string> _events = new();

        public readonly ManualResetEventSlim Connected = new(false);
        public readonly ManualResetEventSlim Disconnected = new(false);
        public readonly ManualResetEventSlim Stopped = new(false);
        public readonly ManualResetEventSlim ReceivedCount = new(false);

        public int ConnectedCount { get; private set; }

        public int DisconnectedCount { get; private set; }

        public int StoppedCount { get; private set; }

        public IRawReliableEndpoint? Endpoint { get; private set; }

        public StopReason? DisconnectedReason { get; private set; }

        public StopReason? StoppedReason { get; private set; }

        public bool DisconnectOnConnected { get; set; }

        /// <summary>
        /// Gets the ordered lifecycle callbacks observed by the handler.
        /// </summary>
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

        /// <summary>
        /// Gets the application values delivered to the handler.
        /// </summary>
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
                    if (receivedBuffer.Elements.Count > 0)
                    {
                        _receivedValues.Add(receivedBuffer.Elements[0].Alias.IntValue);
                    }
                }

                ReceivedCount.Set();
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

            Disconnected.Set();
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
