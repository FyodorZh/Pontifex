using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Actuarius.Memory;
using NUnit.Framework;
using Pontifex.NetSockets;
using Pontifex.RR.Unreliable;
using Pontifex.RR.Unreliable.Udp;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Tests.RR.Unreliable;

/// <summary>
/// End-to-end smoke test of the RR unreliable UDP transport: one request is sent, the server answers by
/// request id, and the client receives the matched response.
/// </summary>
[TestFixture]
public sealed class RRUnreliableUdpRoundTripTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [Test]
    public void RequestAndResponse_RoundTripThroughUdp()
    {
        int port = GetFreeLoopbackUdpPort();
        ILogger logger = new Logger([]);
        var serverEndPoint = new IpEndPoint(new IPEndPoint(IPAddress.Loopback, port));

        var server = new RRUnreliableUdpServer(
            IPAddress.Loopback, port, TimeSpan.FromSeconds(10), logger, MemoryRental.Shared);
        var client = new RRUnreliableUdpClient(
            serverEndPoint, TimeSpan.FromSeconds(10), logger, MemoryRental.Shared);

        using var serverHandler = new EchoServerHandler(server, MemoryRental.Shared);
        using var clientHandler = new RecordingClientHandler();

        try
        {
            Assert.That(client.ServerEndPoint, Is.SameAs(serverEndPoint));
            Assert.That(client.MessageMaxByteSize, Is.EqualTo(RRUdpInfo.MessageMaxByteSize));
            Assert.That(server.MessageMaxByteSize, Is.EqualTo(RRUdpInfo.MessageMaxByteSize));

            Assert.That(server.Init(serverHandler), Is.True);
            Assert.That(server.Start(_ => { }), Is.True);
            Assert.That(client.Init(clientHandler), Is.True);
            Assert.That(client.Start(_ => { }), Is.True);

            Assert.That(serverHandler.StartedSignal.Wait(Timeout), Is.True,
                "the server handler must be started");
            Assert.That(clientHandler.StartedSignal.Wait(Timeout), Is.True,
                "the client handler must be started");

            var request = MemoryRental.Shared.CollectablePool.Acquire<UnionDataList>();
            request.PutLast(42);

            Assert.That(client.Request(request, out long requestId), Is.EqualTo(SendResult.Ok));
            Assert.That(requestId, Is.EqualTo(1));

            Assert.That(serverHandler.RequestedSignal.Wait(Timeout), Is.True,
                "the server must receive the request");
            Assert.That(serverHandler.LastRequestId, Is.EqualTo(1));
            Assert.That(serverHandler.LastClient, Is.Not.Null);
            Assert.That(serverHandler.LastRequestValue, Is.EqualTo(42));

            Assert.That(clientHandler.RespondedSignal.Wait(Timeout), Is.True,
                "the client must receive the matched response");
            Assert.That(clientHandler.ResponseRequestId, Is.EqualTo(requestId));
            Assert.That(clientHandler.ResponseValue, Is.EqualTo(43));
        }
        finally
        {
            client.Stop();
            server.Stop();
        }
    }

    [Test]
    public void Response_WithUnknownRequestId_ReturnsInvalidAddress()
    {
        int port = GetFreeLoopbackUdpPort();
        ILogger logger = new Logger([]);

        var server = new RRUnreliableUdpServer(
            IPAddress.Loopback, port, TimeSpan.FromSeconds(10), logger, MemoryRental.Shared);
        using var serverHandler = new EchoServerHandler(server, MemoryRental.Shared);

        try
        {
            Assert.That(server.Init(serverHandler), Is.True);
            Assert.That(server.Start(_ => { }), Is.True);

            var response = MemoryRental.Shared.CollectablePool.Acquire<UnionDataList>();
            response.PutLast(1);

            Assert.That(server.Response(1, response), Is.EqualTo(SendResult.InvalidAddress));
        }
        finally
        {
            server.Stop();
        }
    }

    [Test]
    public void RejectedRequest_CannotBeResponded()
    {
        int port = GetFreeLoopbackUdpPort();
        ILogger logger = new Logger([]);
        var serverEndPoint = new IpEndPoint(new IPEndPoint(IPAddress.Loopback, port));

        var server = new RRUnreliableUdpServer(
            IPAddress.Loopback, port, TimeSpan.FromSeconds(10), logger, MemoryRental.Shared);
        var client = new RRUnreliableUdpClient(
            serverEndPoint, TimeSpan.FromSeconds(10), logger, MemoryRental.Shared);

        using var serverHandler = new EchoServerHandler(server, MemoryRental.Shared, accept: false);
        using var clientHandler = new RecordingClientHandler();

        try
        {
            Assert.That(server.Init(serverHandler), Is.True);
            Assert.That(server.Start(_ => { }), Is.True);
            Assert.That(client.Init(clientHandler), Is.True);
            Assert.That(client.Start(_ => { }), Is.True);

            var first = MemoryRental.Shared.CollectablePool.Acquire<UnionDataList>();
            first.PutLast(1);
            Assert.That(client.Request(first, out _), Is.EqualTo(SendResult.Ok));

            var second = MemoryRental.Shared.CollectablePool.Acquire<UnionDataList>();
            second.PutLast(2);
            Assert.That(client.Request(second, out _), Is.EqualTo(SendResult.Ok));

            // The server receiver is serial: once both rejected requests reached the handler, the first
            // one's pending route has already been removed.
            Assert.That(SpinWait.SpinUntil(() => serverHandler.RequestCount >= 2, Timeout), Is.True);

            var response = MemoryRental.Shared.CollectablePool.Acquire<UnionDataList>();
            response.PutLast(3);
            Assert.That(
                server.Response(serverHandler.FirstRequestId, response),
                Is.EqualTo(SendResult.InvalidAddress));
        }
        finally
        {
            client.Stop();
            server.Stop();
        }
    }

    private static int GetFreeLoopbackUdpPort()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        return ((IPEndPoint)socket.LocalEndPoint!).Port;
    }

    private sealed class EchoServerHandler : IRRUnreliableServerHandler, IDisposable
    {
        private readonly IRRUnreliableServer _server;
        private readonly IMemoryRental _memory;
        private readonly bool _accept;
        private readonly object _locker = new();
        private int _requestCount;
        private long _firstRequestId;

        public EchoServerHandler(IRRUnreliableServer server, IMemoryRental memory, bool accept = true)
        {
            _server = server;
            _memory = memory;
            _accept = accept;
        }

        public ManualResetEventSlim StartedSignal { get; } = new(false);

        public ManualResetEventSlim RequestedSignal { get; } = new(false);

        public long LastRequestId { get; private set; }

        public IEndPoint? LastClient { get; private set; }

        public int LastRequestValue { get; private set; }

        public int RequestCount
        {
            get
            {
                lock (_locker)
                {
                    return _requestCount;
                }
            }
        }

        public long FirstRequestId
        {
            get
            {
                lock (_locker)
                {
                    return _firstRequestId;
                }
            }
        }

        public void Started()
        {
            StartedSignal.Set();
        }

        public bool OnRequested(long requestId, IEndPoint client, UnionDataList request)
        {
            lock (_locker)
            {
                _requestCount++;
                if (_requestCount == 1)
                {
                    _firstRequestId = requestId;
                }

                LastRequestId = requestId;
                LastClient = client;
                LastRequestValue = request.Elements[0].Alias.IntValue;
            }

            try
            {
                if (!_accept)
                {
                    RequestedSignal.Set();
                    return false;
                }

                var response = _memory.CollectablePool.Acquire<UnionDataList>();
                response.PutLast(LastRequestValue + 1);
                Assert.That(_server.Response(requestId, response), Is.EqualTo(SendResult.Ok));

                RequestedSignal.Set();
                return true;
            }
            finally
            {
                request.Release();
            }
        }

        public void Stopped()
        {
        }

        public void Dispose()
        {
            StartedSignal.Dispose();
            RequestedSignal.Dispose();
        }
    }

    private sealed class RecordingClientHandler : IRRUnreliableClientHandler, IDisposable
    {
        public ManualResetEventSlim StartedSignal { get; } = new(false);

        public ManualResetEventSlim RespondedSignal { get; } = new(false);

        public long ResponseRequestId { get; private set; }

        public int ResponseValue { get; private set; }

        public void Started()
        {
            StartedSignal.Set();
        }

        public void OnResponded(long requestId, UnionDataList response)
        {
            try
            {
                ResponseRequestId = requestId;
                ResponseValue = response.Elements[0].Alias.IntValue;
                RespondedSignal.Set();
            }
            finally
            {
                response.Release();
            }
        }

        public void Stopped()
        {
        }

        public void Dispose()
        {
            StartedSignal.Dispose();
            RespondedSignal.Dispose();
        }
    }
}
