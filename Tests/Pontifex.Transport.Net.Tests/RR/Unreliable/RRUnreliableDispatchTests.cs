using System;
using System.Collections.Generic;
using System.Net;
using Actuarius.Memory;
using NUnit.Framework;
using Operarius;
using Pontifex.NetSockets;
using Pontifex.RR;
using Pontifex.RR.Unreliable;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Tests.RR.Unreliable;

/// <summary>
/// Deterministic checks of the RR unreliable client and server dispatch rules that do not need a
/// carrier: route filtering, inbound payload limits, the response message sequence, and the handle
/// contract.
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

            client.Receive(foreign, Response(requestId, 0, 2, 2));

            Assert.That(handler.Responded, Is.False);

            client.Receive(server, Response(requestId, 0, 1, 3));

            Assert.That(handler.Responded, Is.True);
            Assert.That(handler.ResponseValue, Is.EqualTo(3));
            Assert.That(handler.MessageCounts, Is.EqualTo(new[] { 1 }));
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

            client.Receive(server, Response(requestId, 0, 2, 1, 2, 3));

            Assert.That(handler.Responded, Is.False);

            client.Receive(server, Response(requestId, 0, 1, 4));

            Assert.That(handler.Responded, Is.True);
            Assert.That(handler.ResponseValue, Is.EqualTo(4));
            Assert.That(handler.MessageCounts, Is.EqualTo(new[] { 1 }));
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

    [Test]
    public void ResponseSequence_IsDeliveredWithIdsAndCount()
    {
        IpEndPoint server = Endpoint(5000);
        var client = new TestClientTransport(server, 100);
        var handler = new RecordingHandler();

        try
        {
            Assert.That(client.Start(_ => { }), Is.True);
            ushort requestId = SendRequest(client, handler);

            client.Receive(server, Response(requestId, 1, 3, 11));
            client.Receive(server, Response(requestId, 0, 3, 10));
            client.Receive(server, Response(requestId, 2, 3, 12));

            Assert.Multiple(() =>
            {
                Assert.That(handler.Values, Is.EqualTo(new[] { 11, 10, 12 }));
                Assert.That(handler.MessageIds, Is.EqualTo(new[] { 1, 0, 2 }));
                Assert.That(handler.MessageCounts, Is.EqualTo(new[] { 3, 3, 3 }));
            });
        }
        finally
        {
            client.Stop();
        }
    }

    [Test]
    public void DuplicateMessageId_IsDeliveredOnce()
    {
        IpEndPoint server = Endpoint(5000);
        var client = new TestClientTransport(server, 100);
        var handler = new RecordingHandler();

        try
        {
            Assert.That(client.Start(_ => { }), Is.True);
            ushort requestId = SendRequest(client, handler);

            client.Receive(server, Response(requestId, 0, 2, 10));
            client.Receive(server, Response(requestId, 0, 2, 10));
            client.Receive(server, Response(requestId, 1, 2, 11));

            Assert.That(handler.Values, Is.EqualTo(new[] { 10, 11 }));
            Assert.That(handler.ResponseCount, Is.EqualTo(2));
        }
        finally
        {
            client.Stop();
        }
    }

    [Test]
    public void MismatchedMessageCount_IsDropped()
    {
        IpEndPoint server = Endpoint(5000);
        var client = new TestClientTransport(server, 100);
        var handler = new RecordingHandler();

        try
        {
            Assert.That(client.Start(_ => { }), Is.True);
            ushort requestId = SendRequest(client, handler);

            client.Receive(server, Response(requestId, 0, 3, 10));
            client.Receive(server, Response(requestId, 1, 2, 20));
            client.Receive(server, Response(requestId, 1, 3, 11));

            Assert.That(handler.Values, Is.EqualTo(new[] { 10, 11 }));
        }
        finally
        {
            client.Stop();
        }
    }

    [Test]
    public void MessageIdBeyondTheDeclaredCount_IsDropped()
    {
        IpEndPoint server = Endpoint(5000);
        var client = new TestClientTransport(server, 100);
        var handler = new RecordingHandler();

        try
        {
            Assert.That(client.Start(_ => { }), Is.True);
            ushort requestId = SendRequest(client, handler);

            client.Receive(server, RawResponse(requestId, 2, 1, 20));
            client.Receive(server, Response(requestId, 0, 1, 10));

            Assert.That(handler.Values, Is.EqualTo(new[] { 10 }));
            Assert.That(handler.MessageCounts, Is.EqualTo(new[] { 1 }));
        }
        finally
        {
            client.Stop();
        }
    }

    [Test]
    public void LastMessage_UnbindsTheHandler()
    {
        IpEndPoint server = Endpoint(5000);
        var client = new TestClientTransport(server, 100);
        var handler = new RecordingHandler();

        try
        {
            Assert.That(client.Start(_ => { }), Is.True);
            ushort requestId = SendRequest(client, handler);

            client.Receive(server, Response(requestId, 0, 1, 10));

            var next = MemoryRental.Shared.CollectablePool.Acquire<UnionDataList>();
            next.PutLast(2);

            Assert.That(client.Request(next, handler), Is.EqualTo(SendResult.Ok));
        }
        finally
        {
            client.Stop();
        }
    }

    [Test]
    public void ResponseAfterTheLastMessage_IsDropped()
    {
        IpEndPoint server = Endpoint(5000);
        var client = new TestClientTransport(server, 100);
        var handler = new RecordingHandler();

        try
        {
            Assert.That(client.Start(_ => { }), Is.True);
            ushort requestId = SendRequest(client, handler);

            client.Receive(server, Response(requestId, 0, 1, 10));
            client.Receive(server, Response(requestId, 0, 1, 10));

            Assert.That(handler.ResponseCount, Is.EqualTo(1));
        }
        finally
        {
            client.Stop();
        }
    }

    [Test]
    public void MessageCount_DefaultsToOne()
    {
        var server = new TestServerTransport(100);
        var handler = new RecordingServerHandler();

        try
        {
            Assert.That(server.Init(handler), Is.True);
            Assert.That(server.Start(_ => { }), Is.True);
            server.Receive(Endpoint(6000), Request(1));

            Assert.That(handler.LastResponse!.MessageCount, Is.EqualTo(1));
        }
        finally
        {
            server.Stop();
        }
    }

    [Test]
    public void ResponseSequence_AssignsZeroBasedIdsAndClosesAfterTheLast()
    {
        var server = new TestServerTransport(100);
        var handler = new RecordingServerHandler();

        try
        {
            Assert.That(server.Init(handler), Is.True);
            Assert.That(server.Start(_ => { }), Is.True);
            server.Receive(Endpoint(6000), Request(1));

            IRRUnreliableResponse response = handler.LastResponse!;
            response.SetMessageCount(3);

            Assert.That(response.Response(Payload(10)), Is.EqualTo(SendResult.Ok));
            Assert.That(response.Response(Payload(11)), Is.EqualTo(SendResult.Ok));
            Assert.That(response.Response(Payload(12)), Is.EqualTo(SendResult.Ok));

            var fourth = Payload(13);
            Assert.That(response.Response(fourth), Is.EqualTo(SendResult.Error));
            Assert.That(fourth.IsAlive, Is.False);

            Assert.That(server.SentResponses, Is.EqualTo(new[]
            {
                (0, 3), (1, 3), (2, 3)
            }));
        }
        finally
        {
            server.Stop();
        }
    }

    [Test]
    public void ResponseSequence_SendFailureDoesNotAdvanceTheId()
    {
        var server = new TestServerTransport(100);
        var handler = new RecordingServerHandler();

        try
        {
            Assert.That(server.Init(handler), Is.True);
            Assert.That(server.Start(_ => { }), Is.True);
            server.Receive(Endpoint(6000), Request(1));

            IRRUnreliableResponse response = handler.LastResponse!;
            response.SetMessageCount(2);
            server.SendResults.Enqueue(SendResult.Error);

            Assert.That(response.Response(Payload(10)), Is.EqualTo(SendResult.Error));
            Assert.That(response.Response(Payload(11)), Is.EqualTo(SendResult.Ok));
            Assert.That(response.Response(Payload(12)), Is.EqualTo(SendResult.Ok));

            Assert.That(server.SentResponses, Is.EqualTo(new[]
            {
                (0, 2), (0, 2), (1, 2)
            }));
        }
        finally
        {
            server.Stop();
        }
    }

    [Test]
    public void SetMessageCount_SecondCallThrows()
    {
        var response = StartRequest(out TestServerTransport server);

        try
        {
            response.SetMessageCount(2);

            Assert.Throws<InvalidOperationException>(() => response.SetMessageCount(2));
        }
        finally
        {
            server.Stop();
        }
    }

    [Test]
    public void SetMessageCount_AfterTheFirstResponseThrows()
    {
        var response = StartRequest(out TestServerTransport server);

        try
        {
            Assert.That(response.Response(Payload(10)), Is.EqualTo(SendResult.Ok));

            Assert.Throws<InvalidOperationException>(() => response.SetMessageCount(2));
        }
        finally
        {
            server.Stop();
        }
    }

    [Test]
    public void SetMessageCount_OutsideTheByteRangeThrows()
    {
        var response = StartRequest(out TestServerTransport server);

        try
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => response.SetMessageCount(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => response.SetMessageCount(257));
        }
        finally
        {
            server.Stop();
        }
    }

    [Test]
    public void ResponseSequence_SendsTwoHundredFiftySixMessages()
    {
        var response = StartRequest(out TestServerTransport server);

        try
        {
            response.SetMessageCount(256);

            for (int id = 0; id < 256; id++)
            {
                Assert.That(response.Response(Payload(id)), Is.EqualTo(SendResult.Ok));
            }

            Assert.That(server.SentResponses, Has.Count.EqualTo(256));
            Assert.That(server.SentResponses[0], Is.EqualTo((0, 256)));
            Assert.That(server.SentResponses[255], Is.EqualTo((255, 256)));

            var extra = Payload(256);
            Assert.That(response.Response(extra), Is.EqualTo(SendResult.Error));
            Assert.That(extra.IsAlive, Is.False);
        }
        finally
        {
            server.Stop();
        }
    }

    [Test]
    public void Response_InvalidPayloadDoesNotAdvanceTheId()
    {
        var response = StartRequest(out TestServerTransport server);

        try
        {
            response.SetMessageCount(2);

            Assert.That(response.Response(null!), Is.EqualTo(SendResult.InvalidMessage));
            Assert.That(response.Response(Payload(10)), Is.EqualTo(SendResult.Ok));
            Assert.That(server.SentResponses, Is.EqualTo(new[] { (0, 2) }));
        }
        finally
        {
            server.Stop();
        }
    }

    [Test]
    public void Response_TooBigPayloadDoesNotAdvanceTheId()
    {
        var handler = new RecordingServerHandler();
        var server = new TestServerTransport(10);

        try
        {
            Assert.That(server.Init(handler), Is.True);
            Assert.That(server.Start(_ => { }), Is.True);
            server.Receive(Endpoint(6000), Request(1));

            IRRUnreliableResponse response = handler.LastResponse!;
            response.SetMessageCount(2);

            var tooBig = MemoryRental.Shared.CollectablePool.Acquire<UnionDataList>();
            tooBig.PutLast(1);
            tooBig.PutLast(2);
            tooBig.PutLast(3);

            Assert.That(response.Response(tooBig), Is.EqualTo(SendResult.MessageTooBig));
            Assert.That(tooBig.IsAlive, Is.False);
            Assert.That(response.Response(Payload(10)), Is.EqualTo(SendResult.Ok));
            Assert.That(server.SentResponses, Is.EqualTo(new[] { (0, 2) }));
        }
        finally
        {
            server.Stop();
        }
    }

    [Test]
    public void ConcurrentResponses_AssignEachIdOnce()
    {
        var response = StartRequest(out TestServerTransport server);

        try
        {
            response.SetMessageCount(2);

            Task<SendResult> first = Task.Run(() => response.Response(Payload(10)));
            Task<SendResult> second = Task.Run(() => response.Response(Payload(11)));
            Task.WaitAll(first, second);

            Assert.That(new[] { first.Result, second.Result }, Is.All.EqualTo(SendResult.Ok));
            Assert.That(server.SentResponses.Select(entry => entry.MessageId),
                Is.EquivalentTo(new[] { 0, 1 }));
        }
        finally
        {
            server.Stop();
        }
    }

    [Test]
    public void SetMessageCount_AfterTheResponseTtlThrows()
    {
        var clock = new MutableClock { Now = DateTime.UtcNow };
        var handler = new RecordingServerHandler();
        var server = new TestServerTransport(100, clock);

        try
        {
            Assert.That(server.Init(handler), Is.True);
            Assert.That(server.Start(_ => { }), Is.True);
            server.Receive(Endpoint(6000), Request(1));

            IRRUnreliableResponse response = handler.LastResponse!;
            clock.Now += Ttl + TimeSpan.FromSeconds(1);

            Assert.Throws<InvalidOperationException>(() => response.SetMessageCount(2));
        }
        finally
        {
            server.Stop();
        }
    }

    [Test]
    public void SetMessageCount_AfterStopThrowsAndResponseReturnsError()
    {
        var response = StartRequest(out TestServerTransport server);
        server.Stop();

        Assert.Throws<InvalidOperationException>(() => response.SetMessageCount(2));

        var payload = Payload(10);
        Assert.That(response.Response(payload), Is.EqualTo(SendResult.Error));
        Assert.That(payload.IsAlive, Is.False);
    }

    [Test]
    public void ResponseBeforeBind_IsDroppedAndTheHandlerStaysUsable()
    {
        IpEndPoint server = Endpoint(5000);
        var client = new TestClientTransport(server, 100);
        var handler = new RecordingHandler();

        try
        {
            Assert.That(client.Start(_ => { }), Is.True);

            ushort requestId = 0;
            client.AfterAddBeforeBindForTests = id =>
            {
                requestId = id;
                client.Receive(server, Response(id, 0, 1, 10));
            };

            var request = MemoryRental.Shared.CollectablePool.Acquire<UnionDataList>();
            request.PutLast(1);

            Assert.That(client.Request(request, handler), Is.EqualTo(SendResult.Ok));
            Assert.That(handler.ResponseCount, Is.Zero);
            Assert.That(handler.IsBound, Is.True);

            // The dropped frame must not have fixed the count or marked the id as seen.
            client.Receive(server, Response(requestId, 0, 2, 10));

            Assert.That(handler.ResponseCount, Is.EqualTo(1));
            Assert.That(handler.MessageCounts, Is.EqualTo(new[] { 2 }));

            handler.Cancel();
            Assert.That(handler.IsBound, Is.False);
        }
        finally
        {
            client.Stop();
        }
    }

    [Test]
    public void CancelFromTheResponseCallback_UnbindsAfterItReturns()
    {
        IpEndPoint server = Endpoint(5000);
        var client = new TestClientTransport(server, 100);
        var handler = new RecordingHandler();

        try
        {
            Assert.That(client.Start(_ => { }), Is.True);
            ushort requestId = SendRequest(client, handler);
            handler.OnResponse = handler.Cancel;

            client.Receive(server, Response(requestId, 0, 2, 10));

            Assert.That(handler.RequestId, Is.EqualTo(requestId));
            Assert.That(handler.IsBound, Is.False);

            client.Receive(server, Response(requestId, 1, 2, 11));

            Assert.That(handler.ResponseCount, Is.EqualTo(1));
        }
        finally
        {
            client.Stop();
        }
    }

    [Test]
    public void StopFromTheResponseCallback_UnbindsAndDoesNotDeadlock()
    {
        IpEndPoint server = Endpoint(5000);
        var client = new TestClientTransport(server, 100);
        var handler = new RecordingHandler();

        try
        {
            Assert.That(client.Start(_ => { }), Is.True);
            ushort requestId = SendRequest(client, handler);
            handler.OnResponse = () => client.Stop();

            client.Receive(server, Response(requestId, 0, 1, 10));

            Assert.That(client.IsStarted, Is.False);
            Assert.That(handler.IsBound, Is.False);
        }
        finally
        {
            client.Stop();
        }
    }

    [Test]
    public void SetMessageCount_AfterRejectionThrows()
    {
        var server = new TestServerTransport(100);
        var handler = new RecordingServerHandler { Accept = false };

        try
        {
            Assert.That(server.Init(handler), Is.True);
            Assert.That(server.Start(_ => { }), Is.True);
            server.Receive(Endpoint(6000), Request(1));

            Assert.Throws<InvalidOperationException>(
                () => handler.LastResponse!.SetMessageCount(2));
        }
        finally
        {
            server.Stop();
        }
    }

    [Test]
    public void Request_WithAnUnserializablePayload_ReleasesIt()
    {
        var client = new TestClientTransport(Endpoint(5000), 100);
        var handler = new RecordingHandler();

        try
        {
            Assert.That(client.Start(_ => { }), Is.True);
            var payload = MemoryRental.Shared.CollectablePool.Acquire<UnionDataList>();
            UnionData invalid = default;
            payload.PutLast(invalid);

            Assert.That(client.Request(payload, handler), Is.EqualTo(SendResult.InvalidMessage));
            Assert.That(payload.IsAlive, Is.False);
        }
        finally
        {
            client.Stop();
        }
    }

    [Test]
    public void Response_WithAnUnserializablePayload_ReleasesIt()
    {
        var server = new TestServerTransport(100);
        var handler = new RecordingServerHandler();

        try
        {
            Assert.That(server.Init(handler), Is.True);
            Assert.That(server.Start(_ => { }), Is.True);
            server.Receive(Endpoint(6000), Request(1));

            var payload = MemoryRental.Shared.CollectablePool.Acquire<UnionDataList>();
            UnionData invalid = default;
            payload.PutLast(invalid);

            Assert.That(handler.LastResponse!.Response(payload),
                Is.EqualTo(SendResult.InvalidMessage));
            Assert.That(payload.IsAlive, Is.False);
        }
        finally
        {
            server.Stop();
        }
    }

    [Test]
    public void StopBetweenAddAndBind_DoesNotLeaveTheHandlerBound()
    {
        IpEndPoint server = Endpoint(5000);
        var client = new TestClientTransport(server, 100);
        var handler = new RecordingHandler();

        try
        {
            Assert.That(client.Start(_ => { }), Is.True);

            client.AfterAddBeforeBindForTests = _ => client.Stop();

            var request = MemoryRental.Shared.CollectablePool.Acquire<UnionDataList>();
            request.PutLast(1);

            Assert.That(client.Request(request, handler), Is.EqualTo(SendResult.NotConnected));
            Assert.That(handler.IsBound, Is.False);
            Assert.That(client.PendingCountForTests, Is.Zero);
        }
        finally
        {
            client.Stop();
        }
    }

    [Test]
    public void TryUnbind_DoesNotClearANewerBinding()
    {
        var handler = new RecordingHandler();
        Assert.That(handler.TryReserve(), Is.True);
        handler.Bind(1, _ => { });
        Assert.That(handler.TryUnbind(1), Is.True);
        Assert.That(handler.IsBound, Is.False);

        Assert.That(handler.TryReserve(), Is.True);
        handler.Bind(2, _ => { });
        Assert.That(handler.TryUnbind(1), Is.False);
        Assert.That(handler.IsBound, Is.True);

        Assert.That(handler.TryUnbind(2), Is.True);
        Assert.That(handler.IsBound, Is.False);
    }

    [Test]
    public void TryUnbind_DuringANewReservation_DoesNotClearIt()
    {
        var handler = new RecordingHandler();
        Assert.That(handler.TryReserve(), Is.True);
        handler.Bind(1, _ => { });
        Assert.That(handler.TryUnbind(1), Is.True);

        Assert.That(handler.TryReserve(), Is.True);
        Assert.That(handler.TryUnbind(1), Is.False);
        Assert.That(handler.IsBound, Is.True);

        handler.Bind(2, _ => { });
        Assert.That(handler.TryUnbind(2), Is.True);
    }

    private static IRRUnreliableResponse StartRequest(out TestServerTransport server)
    {
        var handler = new RecordingServerHandler();
        server = new TestServerTransport(100);

        Assert.That(server.Init(handler), Is.True);
        Assert.That(server.Start(_ => { }), Is.True);
        server.Receive(Endpoint(6000), Request(1));

        return handler.LastResponse!;
    }

    private static ushort SendRequest(TestClientTransport client, RecordingHandler handler)
    {
        var request = MemoryRental.Shared.CollectablePool.Acquire<UnionDataList>();
        request.PutLast(1);

        Assert.That(client.Request(request, handler), Is.EqualTo(SendResult.Ok));
        Assert.That(handler.RequestId, Is.Not.Null);

        return handler.RequestId!.Value;
    }

    private static UnionDataList Payload(int value)
    {
        var payload = MemoryRental.Shared.CollectablePool.Acquire<UnionDataList>();
        payload.PutLast(value);
        return payload;
    }

    private static UnionDataList Request(params int[] values)
    {
        var request = MemoryRental.Shared.CollectablePool.Acquire<UnionDataList>();

        foreach (int value in values)
        {
            request.PutLast(value);
        }

        RRUnreliableFrame.EncodeRequest(request, 1);
        return request;
    }

    private static UnionDataList Response(ushort requestId, int messageId, int messageCount,
        params int[] values)
    {
        var response = MemoryRental.Shared.CollectablePool.Acquire<UnionDataList>();

        foreach (int value in values)
        {
            response.PutLast(value);
        }

        RRUnreliableFrame.EncodeResponse(response, requestId, messageId, messageCount);
        return response;
    }

    private static UnionDataList RawResponse(ushort requestId, int messageId, int lastMessageId,
        params int[] values)
    {
        var response = MemoryRental.Shared.CollectablePool.Acquire<UnionDataList>();

        foreach (int value in values)
        {
            response.PutLast(value);
        }

        response.PutFirst(new UnionData((byte)lastMessageId));
        response.PutFirst(new UnionData((byte)messageId));
        response.PutFirst(new UnionData(requestId));
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

        public TestServerTransport(int maxByteSize, IDateTimeProvider? timeProvider = null)
            : base("test-rr-server", Ttl, new Logger([]), MemoryRental.Shared, timeProvider)
        {
            _maxByteSize = maxByteSize;
        }

        public override int MessageMaxByteSize => _maxByteSize;

        public Queue<SendResult> SendResults { get; } = new Queue<SendResult>();

        public List<(int MessageId, int MessageCount)> SentResponses { get; } =
            new List<(int MessageId, int MessageCount)>();

        protected override bool StartCarrier()
        {
            return true;
        }

        protected override void StopCarrier(StopReason reason)
        {
        }

        protected override SendResult SendFrame(IEndPoint destination, UnionDataList frame)
        {
            if (RRUnreliableFrame.TryDecodeResponse(frame, out _, out int messageId,
                    out int messageCount))
            {
                SentResponses.Add((messageId, messageCount));
            }

            frame.Release();
            return SendResults.Count > 0 ? SendResults.Dequeue() : SendResult.Ok;
        }

        public void Receive(IEndPoint source, UnionDataList frame)
        {
            OnFrameReceived(source, frame);
        }
    }

    private sealed class MutableClock : IDateTimeProvider
    {
        public DateTime Now { get; set; }
    }

    private sealed class RecordingHandler : RRRequestHandler, IRRUnreliableRequestHandler
    {
        public int ResponseCount { get; private set; }

        public bool Responded => ResponseCount > 0;

        public int ResponseValue { get; private set; }

        public List<int> Values { get; } = new List<int>();

        public List<int> MessageIds { get; } = new List<int>();

        public List<int> MessageCounts { get; } = new List<int>();

        public Action? OnResponse { get; set; }

        public void Response(UnionDataList response, int messageId, int messageCount)
        {
            try
            {
                int value = response.Elements[0].Alias.IntValue;
                ResponseCount++;
                ResponseValue = value;
                Values.Add(value);
                MessageIds.Add(messageId);
                MessageCounts.Add(messageCount);
                OnResponse?.Invoke();
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

        public IRRUnreliableResponse? LastResponse { get; private set; }

        public bool Accept { get; set; } = true;

        public bool OnRequested(IEndPoint client, UnionDataList request, IRRUnreliableResponse response)
        {
            try
            {
                RequestCount++;
                LastValue = request.Elements[0].Alias.IntValue;
                LastResponse = response;
                return Accept;
            }
            finally
            {
                request.Release();
            }
        }
    }
}
