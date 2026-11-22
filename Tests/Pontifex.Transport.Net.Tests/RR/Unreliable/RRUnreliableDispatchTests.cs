using System;
using System.Net;
using Actuarius.Memory;
using NUnit.Framework;
using Pontifex.NetSockets;
using Pontifex.RR;
using Pontifex.RR.Unreliable;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Tests.RR.Unreliable;

/// <summary>
/// Deterministic checks of the RR unreliable client and server dispatch rules that do not need a
/// carrier: server-route filtering and inbound payload limits.
/// </summary>
[TestFixture]
public sealed class RRUnreliableDispatchTests
{
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(10);

    [Test]
    public void Response_FromForeignRoute_IsDropped()
    {
        IpEndPoint server = Endpoint(5000);
        IpEndPoint foreign = Endpoint(5001);
        var client = new TestClientTransport(server, 100);
        var handler = new RecordingHandler();

        try
        {
            Assert.That(client.Start(_ => { }), Is.True);
            ushort requestId = SendRequest(client, handler);

            client.Receive(foreign, Response(requestId, 2));

            Assert.That(handler.Responded, Is.False);

            client.Receive(server, Response(requestId, 3));

            Assert.That(handler.Responded, Is.True);
            Assert.That(handler.ResponseValue, Is.EqualTo(3));
        }
        finally
        {
            client.Stop();
        }
    }

    [Test]
    public void Response_AboveMessageMaxByteSize_IsDropped()
    {
        IpEndPoint server = Endpoint(5000);
        var client = new TestClientTransport(server, 10);
        var handler = new RecordingHandler();

        try
        {
            Assert.That(client.Start(_ => { }), Is.True);
            ushort requestId = SendRequest(client, handler);

            client.Receive(server, Response(requestId, 1, 2, 3));

            Assert.That(handler.Responded, Is.False);

            client.Receive(server, Response(requestId, 4));

            Assert.That(handler.Responded, Is.True);
            Assert.That(handler.ResponseValue, Is.EqualTo(4));
        }
        finally
        {
            client.Stop();
        }
    }

    [Test]
    public void Request_AboveMessageMaxByteSize_IsNotDispatched()
    {
        var server = new TestServerTransport(10);
        var handler = new RecordingServerHandler();

        try
        {
            Assert.That(server.Init(handler), Is.True);
            Assert.That(server.Start(_ => { }), Is.True);

            server.Receive(Endpoint(6000), Request(1, 2, 3));

            Assert.That(handler.RequestCount, Is.Zero);

            server.Receive(Endpoint(6000), Request(4));

            Assert.That(handler.RequestCount, Is.EqualTo(1));
            Assert.That(handler.LastValue, Is.EqualTo(4));
        }
        finally
        {
            server.Stop();
        }
    }

    private static ushort SendRequest(TestClientTransport client, RecordingHandler handler)
    {
        var request = MemoryRental.Shared.CollectablePool.Acquire<UnionDataList>();
        request.PutLast(1);

        Assert.That(client.Request(request, handler), Is.EqualTo(SendResult.Ok));
        Assert.That(handler.RequestId, Is.Not.Null);

        return handler.RequestId!.Value;
    }

    private static UnionDataList Request(params int[] values)
    {
        var request = MemoryRental.Shared.CollectablePool.Acquire<UnionDataList>();

        foreach (int value in values)
        {
            request.PutLast(value);
        }

        RRUnreliableFrame.Encode(request, 1);
        return request;
    }

    private static UnionDataList Response(ushort requestId, params int[] values)
    {
        var response = MemoryRental.Shared.CollectablePool.Acquire<UnionDataList>();

        foreach (int value in values)
        {
            response.PutLast(value);
        }

        RRUnreliableFrame.Encode(response, requestId);
        return response;
    }

    private static IpEndPoint Endpoint(int port)
    {
        return new IpEndPoint(new IPEndPoint(IPAddress.Loopback, port));
    }

    private sealed class TestClientTransport : RRUnreliableClientTransport
    {
        private readonly int _maxByteSize;

        public TestClientTransport(IEndPoint serverEndPoint, int maxByteSize)
            : base("test-rr-client", serverEndPoint, Ttl, new Logger([]), MemoryRental.Shared)
        {
            _maxByteSize = maxByteSize;
        }

        public override int MessageMaxByteSize => _maxByteSize;

        protected override bool StartCarrier()
        {
            return true;
        }

        protected override void StopCarrier(StopReason reason)
        {
        }

        protected override SendResult SendFrame(UnionDataList frame)
        {
            frame.Release();
            return SendResult.Ok;
        }

        public void Receive(IEndPoint source, UnionDataList frame)
        {
            OnFrameReceived(source, frame);
        }
    }

    private sealed class TestServerTransport : RRUnreliableServerTransport
    {
        private readonly int _maxByteSize;

        public TestServerTransport(int maxByteSize)
            : base("test-rr-server", Ttl, new Logger([]), MemoryRental.Shared)
        {
            _maxByteSize = maxByteSize;
        }

        public override int MessageMaxByteSize => _maxByteSize;

        protected override bool StartCarrier()
        {
            return true;
        }

        protected override void StopCarrier(StopReason reason)
        {
        }

        protected override SendResult SendFrame(IEndPoint destination, UnionDataList frame)
        {
            frame.Release();
            return SendResult.Ok;
        }

        public void Receive(IEndPoint source, UnionDataList frame)
        {
            OnFrameReceived(source, frame);
        }
    }

    private sealed class RecordingHandler : RRRequestHandler, IRRUnreliableRequestHandler
    {
        public bool Responded { get; private set; }

        public int ResponseValue { get; private set; }

        public void Response(UnionDataList response)
        {
            try
            {
                ResponseValue = response.Elements[0].Alias.IntValue;
                Responded = true;
            }
            finally
            {
                response.Release();
            }
        }
    }

    private sealed class RecordingServerHandler : IRRUnreliableServerHandler
    {
        public int RequestCount { get; private set; }

        public int LastValue { get; private set; }

        public bool OnRequested(IEndPoint client, UnionDataList request, IRRUnreliableResponse response)
        {
            try
            {
                RequestCount++;
                LastValue = request.Elements[0].Alias.IntValue;
                return true;
            }
            finally
            {
                request.Release();
            }
        }
    }
}
