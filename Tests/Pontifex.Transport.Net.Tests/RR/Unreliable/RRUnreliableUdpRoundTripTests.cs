using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Actuarius.Memory;
using NUnit.Framework;
using Pontifex.NetSockets;
using Pontifex.RR;
using Pontifex.RR.Unreliable;
using Pontifex.RR.Unreliable.Udp;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Tests.RR.Unreliable;

/// <summary>
/// End-to-end smoke test of the RR unreliable UDP transport: a request is sent through a per-request
/// handler, the server answers through the response handle, and the client receives the matched
/// response.
/// </summary>
[TestFixture]
public sealed class RRUnreliableUdpRoundTripTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [Test]
    public void MessageMaxByteSize_UsesTheMinimumIpv4DatagramBudget()
    {
        Assert.That(RRUnreliableFrame.OverheadBytes, Is.EqualTo(7));
        Assert.That(RRUdpInfo.MessageMaxByteSize, Is.EqualTo(540));
        Assert.That(RRUdpInfo.MessageMaxByteSize + RRUnreliableFrame.OverheadBytes + 1,
            Is.EqualTo(548));
    }

    [Test]
    public void EncodedFramesAtThePayloadLimit_FitTheMinimumDatagram()
    {
        var response = PayloadOfSize(RRUdpInfo.MessageMaxByteSize);

        try
        {
            Assert.That(response.GetDataSize(), Is.EqualTo(RRUdpInfo.MessageMaxByteSize));

            RRUnreliableFrame.EncodeResponse(response, 1, 0, 1);

            Assert.That(response.GetDataSize(), Is.LessThanOrEqualTo(548));
        }
        finally
        {
            response.Release();
        }

        var request = PayloadOfSize(RRUdpInfo.MessageMaxByteSize);

        try
        {
            RRUnreliableFrame.EncodeRequest(request, 1);

            Assert.That(request.GetDataSize(), Is.LessThanOrEqualTo(548));
        }
        finally
        {
            request.Release();
        }
    }

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

        using var serverHandler = new TestServerHandler(MemoryRental.Shared, respond: true);
        using var clientHandler = new RecordingClientHandler();

        try
        {
            Assert.That(client.ServerEndPoint, Is.SameAs(serverEndPoint));
            Assert.That(client.MessageMaxByteSize, Is.EqualTo(RRUdpInfo.MessageMaxByteSize));
            Assert.That(server.MessageMaxByteSize, Is.EqualTo(RRUdpInfo.MessageMaxByteSize));
            Assert.That(client.RequestTtl, Is.EqualTo(TimeSpan.FromSeconds(10)));
            Assert.That(server.ResponseTtl, Is.EqualTo(TimeSpan.FromSeconds(10)));

            Assert.That(server.Init(serverHandler), Is.True);
            Assert.That(server.Start(_ => { }), Is.True);
            Assert.That(client.Start(_ => { }), Is.True);

            var request = MemoryRental.Shared.CollectablePool.Acquire<UnionDataList>();
            request.PutLast(42);

            Assert.That(client.Request(request, clientHandler), Is.EqualTo(SendResult.Ok));
            Assert.That(clientHandler.RequestId, Is.Not.Null);

            Assert.That(serverHandler.RequestedSignal.Wait(Timeout), Is.True,
                "the server must receive the request");
            Assert.That(serverHandler.LastRequestId, Is.EqualTo(1));
            Assert.That(serverHandler.LastClient, Is.Not.Null);
            Assert.That(serverHandler.LastRequestValue, Is.EqualTo(42));
            Assert.That(serverHandler.ResponseResult, Is.EqualTo(SendResult.Ok));

            Assert.That(clientHandler.RespondedSignal.Wait(Timeout), Is.True,
                "the client must receive the matched response");
            Assert.That(clientHandler.ResponseValue, Is.EqualTo(43));
        }
        finally
        {
            client.Stop();
            server.Stop();
        }
    }

    [Test]
    public void ResponseHandle_SendsDeclaredSequenceAndCloses()
    {
        int port = GetFreeLoopbackUdpPort();
        ILogger logger = new Logger([]);
        var serverEndPoint = new IpEndPoint(new IPEndPoint(IPAddress.Loopback, port));

        var server = new RRUnreliableUdpServer(
            IPAddress.Loopback, port, TimeSpan.FromSeconds(10), logger, MemoryRental.Shared);
        var client = new RRUnreliableUdpClient(
            serverEndPoint, TimeSpan.FromSeconds(10), logger, MemoryRental.Shared);

        using var serverHandler = new TestServerHandler(MemoryRental.Shared);
        using var clientHandler = new RecordingClientHandler();

        try
        {
            Assert.That(server.Init(serverHandler), Is.True);
            Assert.That(server.Start(_ => { }), Is.True);
            Assert.That(client.Start(_ => { }), Is.True);

            var request = MemoryRental.Shared.CollectablePool.Acquire<UnionDataList>();
            request.PutLast(1);
            Assert.That(client.Request(request, clientHandler), Is.EqualTo(SendResult.Ok));

            Assert.That(serverHandler.RequestedSignal.Wait(Timeout), Is.True);
            IRRUnreliableResponse response = serverHandler.LastResponse!;
            response.SetMessageCount(2);

            var first = MemoryRental.Shared.CollectablePool.Acquire<UnionDataList>();
            first.PutLast(2);
            Assert.That(response.Response(first), Is.EqualTo(SendResult.Ok));

            var second = MemoryRental.Shared.CollectablePool.Acquire<UnionDataList>();
            second.PutLast(3);
            Assert.That(response.Response(second), Is.EqualTo(SendResult.Ok));

            var third = MemoryRental.Shared.CollectablePool.Acquire<UnionDataList>();
            third.PutLast(4);
            Assert.That(response.Response(third), Is.EqualTo(SendResult.Error));

            Assert.That(clientHandler.LastRespondedSignal.Wait(Timeout), Is.True);
            Assert.That(clientHandler.ResponseCount, Is.EqualTo(2));
            Assert.That(clientHandler.MessageIds, Is.EquivalentTo(new[] { 0, 1 }));
            Assert.That(clientHandler.MessageCounts, Is.All.EqualTo(2));
        }
        finally
        {
            client.Stop();
            server.Stop();
        }
    }

    [Test]
    public void RejectedRequest_ClosesResponseHandle()
    {
        int port = GetFreeLoopbackUdpPort();
        ILogger logger = new Logger([]);
        var serverEndPoint = new IpEndPoint(new IPEndPoint(IPAddress.Loopback, port));

        var server = new RRUnreliableUdpServer(
            IPAddress.Loopback, port, TimeSpan.FromSeconds(10), logger, MemoryRental.Shared);
        var client = new RRUnreliableUdpClient(
            serverEndPoint, TimeSpan.FromSeconds(10), logger, MemoryRental.Shared);

        using var serverHandler = new TestServerHandler(MemoryRental.Shared, accept: false);
        using var clientHandler = new RecordingClientHandler();

        try
        {
            Assert.That(server.Init(serverHandler), Is.True);
            Assert.That(server.Start(_ => { }), Is.True);
            Assert.That(client.Start(_ => { }), Is.True);

            var request = MemoryRental.Shared.CollectablePool.Acquire<UnionDataList>();
            request.PutLast(1);
            Assert.That(client.Request(request, clientHandler), Is.EqualTo(SendResult.Ok));

            Assert.That(serverHandler.RequestedSignal.Wait(Timeout), Is.True);

            var response = MemoryRental.Shared.CollectablePool.Acquire<UnionDataList>();
            response.PutLast(2);
            Assert.That(serverHandler.LastResponse!.Response(response), Is.EqualTo(SendResult.Error));
        }
        finally
        {
            client.Stop();
            server.Stop();
        }
    }

    [Test]
    public void HandlerAlreadyBound_ThrowsUntilCanceled()
    {
        int port = GetFreeLoopbackUdpPort();
        ILogger logger = new Logger([]);
        var serverEndPoint = new IpEndPoint(new IPEndPoint(IPAddress.Loopback, port));

        var client = new RRUnreliableUdpClient(
            serverEndPoint, TimeSpan.FromSeconds(10), logger, MemoryRental.Shared);
        var handler = new RecordingClientHandler();

        try
        {
            Assert.That(client.Start(_ => { }), Is.True);

            var first = MemoryRental.Shared.CollectablePool.Acquire<UnionDataList>();
            first.PutLast(1);
            Assert.That(client.Request(first, handler), Is.EqualTo(SendResult.Ok));

            var second = MemoryRental.Shared.CollectablePool.Acquire<UnionDataList>();
            second.PutLast(2);
            Assert.That(
                () => client.Request(second, handler),
                Throws.TypeOf<InvalidOperationException>());
            second.Release();

            handler.Cancel();

            var third = MemoryRental.Shared.CollectablePool.Acquire<UnionDataList>();
            third.PutLast(3);
            Assert.That(client.Request(third, handler), Is.EqualTo(SendResult.Ok));
        }
        finally
        {
            client.Stop();
            handler.Dispose();
        }
    }

    private static UnionDataList PayloadOfSize(int payloadSize)
    {
        // A one-element list has a one-byte count prefix. The array element costs one type byte,
        // the zig-zag var-int length prefix, and the bytes themselves.
        int arrayLength = payloadSize - 2;
        while (payloadSize - 2 - VarIntSize(arrayLength) != arrayLength)
        {
            arrayLength = payloadSize - 2 - VarIntSize(arrayLength);
        }

        var message = MemoryRental.Shared.CollectablePool.Acquire<UnionDataList>();
        message.PutLast(new UnionData(new MultiRefByteArray(new byte[arrayLength])));

        if (message.GetDataSize() != payloadSize)
        {
            message.Release();
            throw new InvalidOperationException(
                $"Could not build a payload of exactly {payloadSize} bytes.");
        }

        return message;
    }

    private static int VarIntSize(int value)
    {
        uint encoded = unchecked((uint)(value << 1));
        int size = 1;
        while ((encoded >>= 7) != 0)
        {
            size++;
        }

        return size;
    }

    private static int GetFreeLoopbackUdpPort()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        return ((IPEndPoint)socket.LocalEndPoint!).Port;
    }

    private sealed class TestServerHandler : IRRUnreliableServerHandler, IDisposable
    {
        private readonly IMemoryRental _memory;
        private readonly bool _accept;
        private readonly bool _respond;

        public TestServerHandler(IMemoryRental memory, bool accept = true, bool respond = false)
        {
            _memory = memory;
            _accept = accept;
            _respond = respond;
        }

        public ManualResetEventSlim RequestedSignal { get; } = new(false);

        public IRRUnreliableResponse? LastResponse { get; private set; }

        public long LastRequestId { get; private set; }

        public IEndPoint? LastClient { get; private set; }

        public int LastRequestValue { get; private set; }

        public SendResult ResponseResult { get; private set; }

        public bool OnRequested(IEndPoint client, UnionDataList request, IRRUnreliableResponse response)
        {
            LastResponse = response;
            LastRequestId = response.RequestId;
            LastClient = client;
            LastRequestValue = request.Elements[0].Alias.IntValue;

            try
            {
                if (!_accept)
                {
                    return false;
                }

                if (_respond)
                {
                    var payload = _memory.CollectablePool.Acquire<UnionDataList>();
                    payload.PutLast(LastRequestValue + 1);
                    ResponseResult = response.Response(payload);
                }

                return true;
            }
            finally
            {
                request.Release();
                RequestedSignal.Set();
            }
        }

        public void Dispose()
        {
            RequestedSignal.Dispose();
        }
    }

    private sealed class RecordingClientHandler : RRRequestHandler, IRRUnreliableRequestHandler, IDisposable
    {
        public ManualResetEventSlim RespondedSignal { get; } = new(false);

        private int _responseCount;

        public ManualResetEventSlim LastRespondedSignal { get; } = new(false);

        public int ResponseCount => Volatile.Read(ref _responseCount);

        public int ResponseValue { get; private set; }

        public List<int> MessageIds { get; } = new List<int>();

        public List<int> MessageCounts { get; } = new List<int>();

        public void Response(UnionDataList response, int messageId, int messageCount)
        {
            try
            {
                ResponseValue = response.Elements[0].Alias.IntValue;
                Interlocked.Increment(ref _responseCount);
                MessageIds.Add(messageId);
                MessageCounts.Add(messageCount);

                if (messageId == messageCount - 1)
                {
                    LastRespondedSignal.Set();
                }

                RespondedSignal.Set();
            }
            finally
            {
                response.Release();
            }
        }

        public void Dispose()
        {
            RespondedSignal.Dispose();
            LastRespondedSignal.Dispose();
        }
    }
}
