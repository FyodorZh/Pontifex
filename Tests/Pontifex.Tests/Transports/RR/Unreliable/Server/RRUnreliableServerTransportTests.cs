using System;
using System.Linq;
using System.Threading;
using Pontifex.Endpoints;
using Pontifex.RR.Unreliable;
using Pontifex.Tests.Endpoints;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Tests.Transports;

[TestFixture]
[Category("Transports")]
[Category("Transports.RR")]
[Category("Transports.RR.Unreliable")]
[Category("Transports.RR.Unreliable.Server")]
public sealed class RRUnreliableServerTransportTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ResponseTtl = TimeSpan.FromSeconds(3);
    private const int MessageLimit = 64;

    private readonly List<TestRRUnreliableServerTransport> _servers =
        new List<TestRRUnreliableServerTransport>();

    [TearDown]
    public void TearDown()
    {
        foreach (TestRRUnreliableServerTransport server in _servers)
        {
            server.Stop();
        }

        _servers.Clear();
    }

    [Test]
    public void Constructor_WithNonPositiveResponseTtl_Throws()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new TestRRUnreliableServerTransport(TimeSpan.Zero, NewClock()));

        Assert.That(exception!.ParamName, Is.EqualTo("responseTtl"));
    }

    [Test]
    public void Constructor_ExposesResponseTtl()
    {
        var server = new TestRRUnreliableServerTransport(ResponseTtl, NewClock());
        _servers.Add(server);

        Assert.That(server.ResponseTtl, Is.EqualTo(ResponseTtl));
    }

    [Test]
    public void OnFrameReceived_WithNullFrame_DoesNothing()
    {
        var handler = new RecordingUnreliableServerHandler();
        TestRRUnreliableServerTransport server = StartServer(handler, NewClock());

        Assert.DoesNotThrow(() => server.Receive(NewClientRoute(), null!));

        Assert.Multiple(() =>
        {
            Assert.That(handler.RequestCount, Is.Zero);
            Assert.That(server.SendCalls, Is.Zero);
        });
    }

    [Test]
    public void OnFrameReceived_WithMalformedFrame_ReleasesAndDoesNotPresent()
    {
        var handler = new RecordingUnreliableServerHandler();
        TestRRUnreliableServerTransport server = StartServer(handler, NewClock());
        UnionDataList frame = TestMessages.Int(7);

        server.Receive(NewClientRoute(), frame);

        Assert.Multiple(() =>
        {
            Assert.That(frame.RefCount.RefCount, Is.Zero);
            Assert.That(handler.RequestCount, Is.Zero);
        });
    }

    [Test]
    public void OnFrameReceived_WithOversizedFrame_ReleasesAndDoesNotPresent()
    {
        var handler = new RecordingUnreliableServerHandler();
        TestRRUnreliableServerTransport server = StartServer(handler, NewClock());
        UnionDataList frame = TestMessages.PayloadOfSize(server.MessageLimit + 1);
        RRUnreliableFrame.EncodeRequest(frame, 5);

        server.Receive(NewClientRoute(), frame);

        Assert.Multiple(() =>
        {
            Assert.That(frame.RefCount.RefCount, Is.Zero);
            Assert.That(handler.RequestCount, Is.Zero);
            Assert.That(server.SendCalls, Is.Zero);
        });
    }

    [Test]
    public void OnFrameReceived_BeforeStart_ReleasesWithoutPresenting()
    {
        var handler = new RecordingUnreliableServerHandler();
        var server = new TestRRUnreliableServerTransport(ResponseTtl, NewClock());
        _servers.Add(server);
        Assert.That(server.Init(handler), Is.True);
        UnionDataList frame = RequestFrame(1);

        server.Receive(NewClientRoute(), frame);

        Assert.Multiple(() =>
        {
            Assert.That(frame.RefCount.RefCount, Is.Zero);
            Assert.That(handler.RequestCount, Is.Zero);
        });
    }

    [Test]
    public void OnFrameReceived_WhenHandlerNotInitialized_ReleasesWithoutPresenting()
    {
        var server = new TestRRUnreliableServerTransport(ResponseTtl, NewClock());
        _servers.Add(server);
        UnionDataList frame = RequestFrame(1);

        server.Receive(NewClientRoute(), frame);

        Assert.That(frame.RefCount.RefCount, Is.Zero);
    }

    [Test]
    public void OnRequested_WhenHandlerAccepts_KeepsHandleOpen()
    {
        var handler = new RecordingUnreliableServerHandler();
        TestRRUnreliableServerTransport server = StartServer(handler, NewClock());
        server.Receive(NewClientRoute(), RequestFrame(7));
        IRRUnreliableResponse handle = handler.LastHandle!;

        Assert.Multiple(() =>
        {
            Assert.That(handle.MessageCount, Is.EqualTo(1));
            Assert.That(handle.Response(TestMessages.Int(1)), Is.EqualTo(SendResult.Ok));
        });
    }

    [Test]
    public void OnRequested_WhenHandlerRejects_LaterResponseReturnsError()
    {
        var handler = new RecordingUnreliableServerHandler
        {
            Mode = UnreliableServerHandlerMode.Reject
        };
        TestRRUnreliableServerTransport server = StartServer(handler, NewClock());
        server.Receive(NewClientRoute(), RequestFrame(7));
        IRRUnreliableResponse handle = handler.LastHandle!;
        UnionDataList payload = TestMessages.Int(1);

        Assert.That(handle.Response(payload), Is.EqualTo(SendResult.Error));

        Assert.Multiple(() =>
        {
            Assert.That(payload.RefCount.RefCount, Is.Zero);
            Assert.That(server.SendCalls, Is.Zero);
        });
    }

    [Test]
    public void OnRequested_WhenHandlerThrows_LogsRejectsAndTransportStaysValid()
    {
        var consumer = new RecordingLogConsumer();
        var handler = new RecordingUnreliableServerHandler
        {
            Mode = UnreliableServerHandlerMode.Throw
        };
        TestRRUnreliableServerTransport server = StartServer(handler, NewClock(),
            logger: new Logger([consumer]));
        server.Receive(NewClientRoute(), RequestFrame(7));
        IRRUnreliableResponse handle = handler.LastHandle!;
        UnionDataList payload = TestMessages.Int(1);

        SendResult result = handle.Response(payload);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(SendResult.Error));
            Assert.That(payload.RefCount.RefCount, Is.Zero);
            Assert.That(consumer.Messages, Has.Some.Contains("server handler failure"));
            Assert.That(server.IsValid, Is.True);
            Assert.That(server.IsStarted, Is.True);
            Assert.That(server.SendCalls, Is.Zero);
        });
    }

    [Test]
    public void OnRequested_AssignsServerGlobalIdsFromOne()
    {
        var handler = new RecordingUnreliableServerHandler();
        TestRRUnreliableServerTransport server = StartServer(handler, NewClock());
        StringEndPoint client = NewClientRoute();

        server.Receive(client, RequestFrame(10, 1));
        server.Receive(client, RequestFrame(11, 2));

        Assert.Multiple(() =>
        {
            Assert.That(handler.Handles[0].RequestId, Is.EqualTo(1));
            Assert.That(handler.Handles[1].RequestId, Is.EqualTo(2));
        });
    }

    [Test]
    public void Response_SendsClientRequestIdMessageIdAndCount()
    {
        const ushort clientRequestId = 0x1234;
        var handler = new RecordingUnreliableServerHandler();
        TestRRUnreliableServerTransport server = StartServer(handler, NewClock());
        StringEndPoint client = NewClientRoute();
        server.Receive(client, RequestFrame(clientRequestId, 7));
        IRRUnreliableResponse handle = handler.LastHandle!;

        Assert.That(handle.Response(TestMessages.Int(9)), Is.EqualTo(SendResult.Ok));

        RecordedUnreliableFrame sent = server.LastSent!;
        Assert.Multiple(() =>
        {
            Assert.That(sent.Decoded, Is.True);
            Assert.That(sent.Destination, Is.SameAs(client));
            Assert.That(sent.RequestId, Is.EqualTo(clientRequestId));
            Assert.That(sent.MessageId, Is.Zero);
            Assert.That(sent.MessageCount, Is.EqualTo(1));
            Assert.That(sent.PayloadValue, Is.EqualTo(9));
        });
    }

    [Test]
    public void Response_DefaultMessageCountIsOne()
    {
        var handler = new RecordingUnreliableServerHandler();
        TestRRUnreliableServerTransport server = StartServer(handler, NewClock());
        server.Receive(NewClientRoute(), RequestFrame(10));
        IRRUnreliableResponse handle = handler.LastHandle!;

        Assert.That(handle.MessageCount, Is.EqualTo(1));

        Assert.That(handle.Response(TestMessages.Int(1)), Is.EqualTo(SendResult.Ok));

        Assert.That(server.LastSent!.MessageCount, Is.EqualTo(1));
    }

    [Test]
    public void SetMessageCount_AcceptsOneTo256()
    {
        var handler = new RecordingUnreliableServerHandler();
        TestRRUnreliableServerTransport server = StartServer(handler, NewClock());
        StringEndPoint client = NewClientRoute();
        server.Receive(client, RequestFrame(10, 1));
        server.Receive(client, RequestFrame(11, 2));
        IRRUnreliableResponse one = handler.Handles[0];
        IRRUnreliableResponse twoHundredFiftySix = handler.Handles[1];

        Assert.DoesNotThrow(() => one.SetMessageCount(1));
        Assert.DoesNotThrow(() => twoHundredFiftySix.SetMessageCount(256));

        Assert.Multiple(() =>
        {
            Assert.That(one.MessageCount, Is.EqualTo(1));
            Assert.That(twoHundredFiftySix.MessageCount, Is.EqualTo(256));
        });
    }

    [Test]
    public void SetMessageCount_RejectsOutOfRange()
    {
        var handler = new RecordingUnreliableServerHandler
        {
            Mode = UnreliableServerHandlerMode.Reject
        };
        TestRRUnreliableServerTransport server = StartServer(handler, NewClock());
        server.Receive(NewClientRoute(), RequestFrame(10));
        IRRUnreliableResponse closed = handler.LastHandle!;

        var zero = Assert.Throws<ArgumentOutOfRangeException>(() => closed.SetMessageCount(0));
        var tooLarge = Assert.Throws<ArgumentOutOfRangeException>(() => closed.SetMessageCount(257));

        Assert.Multiple(() =>
        {
            Assert.That(zero!.ParamName, Is.EqualTo("messageCount"));
            Assert.That(tooLarge!.ParamName, Is.EqualTo("messageCount"));
        });
    }

    [Test]
    public void SetMessageCount_IsOneTimeBeforeFirstResponse()
    {
        var handler = new RecordingUnreliableServerHandler();
        TestRRUnreliableServerTransport server = StartServer(handler, NewClock());
        server.Receive(NewClientRoute(), RequestFrame(10));
        IRRUnreliableResponse handle = handler.LastHandle!;
        Assert.DoesNotThrow(() => handle.SetMessageCount(2));

        Assert.Throws<InvalidOperationException>(() => handle.SetMessageCount(3));
    }

    [Test]
    public void SetMessageCount_AfterFirstResponseThrows()
    {
        var handler = new RecordingUnreliableServerHandler();
        TestRRUnreliableServerTransport server = StartServer(handler, NewClock());
        server.Receive(NewClientRoute(), RequestFrame(10));
        IRRUnreliableResponse handle = handler.LastHandle!;
        handle.SetMessageCount(2);
        Assert.That(handle.Response(TestMessages.Int(1)), Is.EqualTo(SendResult.Ok));

        Assert.Throws<InvalidOperationException>(() => handle.SetMessageCount(3));
        Assert.That(handle.Response(TestMessages.Int(2)), Is.EqualTo(SendResult.Ok),
            "the handle must still be open, so the second response closes it");
    }

    [Test]
    public void SetMessageCount_AfterCloseThrows()
    {
        var handler = new RecordingUnreliableServerHandler();
        TestRRUnreliableServerTransport server = StartServer(handler, NewClock());
        server.Receive(NewClientRoute(), RequestFrame(10));
        IRRUnreliableResponse handle = handler.LastHandle!;
        Assert.That(handle.Response(TestMessages.Int(1)), Is.EqualTo(SendResult.Ok));

        Assert.Throws<InvalidOperationException>(() => handle.SetMessageCount(2));
    }

    [Test]
    public void SetMessageCount_AfterExpiryThrows()
    {
        ManualClock clock = NewClock();
        var handler = new RecordingUnreliableServerHandler();
        TestRRUnreliableServerTransport server = StartServer(handler, clock);
        server.Receive(NewClientRoute(), RequestFrame(10));
        IRRUnreliableResponse handle = handler.LastHandle!;

        clock.Now += ResponseTtl + TimeSpan.FromTicks(1);

        Assert.Throws<InvalidOperationException>(() => handle.SetMessageCount(2));
    }

    [Test]
    public void SetMessageCount_AfterStopThrows()
    {
        var handler = new RecordingUnreliableServerHandler();
        TestRRUnreliableServerTransport server = StartServer(handler, NewClock());
        server.Receive(NewClientRoute(), RequestFrame(10));
        IRRUnreliableResponse handle = handler.LastHandle!;
        Assert.That(server.Stop(), Is.True);

        Assert.Throws<InvalidOperationException>(() => handle.SetMessageCount(2));
    }

    [Test]
    public void Response_WhenSendFrameReturnsNonOk_DoesNotAdvanceAndCanRetry()
    {
        var handler = new RecordingUnreliableServerHandler();
        TestRRUnreliableServerTransport server = StartServer(handler, NewClock());
        server.Receive(NewClientRoute(), RequestFrame(10));
        IRRUnreliableResponse handle = handler.LastHandle!;
        handle.SetMessageCount(2);
        server.SendResult = SendResult.BufferOverflow;

        Assert.That(handle.Response(TestMessages.Int(10)), Is.EqualTo(SendResult.BufferOverflow));

        server.SendResult = SendResult.Ok;
        Assert.That(handle.Response(TestMessages.Int(20)), Is.EqualTo(SendResult.Ok));
        Assert.That(handle.Response(TestMessages.Int(30)), Is.EqualTo(SendResult.Ok));

        Assert.Multiple(() =>
        {
            Assert.That(server.SentFrames.Select(frame => frame.MessageId),
                Is.EqualTo(new[] { 0, 0, 1 }));
            Assert.That(server.SentFrames.Select(frame => frame.PayloadValue),
                Is.EqualTo(new[] { 10, 20, 30 }));
        });
    }

    [Test]
    public void Response_WhenSendFrameThrows_ReturnsErrorAndCanRetry()
    {
        var handler = new RecordingUnreliableServerHandler();
        TestRRUnreliableServerTransport server = StartServer(handler, NewClock());
        server.Receive(NewClientRoute(), RequestFrame(10));
        IRRUnreliableResponse handle = handler.LastHandle!;
        handle.SetMessageCount(2);
        server.ThrowOnSend = true;

        Assert.That(handle.Response(TestMessages.Int(10)), Is.EqualTo(SendResult.Error));

        server.ThrowOnSend = false;
        Assert.That(handle.Response(TestMessages.Int(20)), Is.EqualTo(SendResult.Ok));

        Assert.Multiple(() =>
        {
            Assert.That(server.SentFrames.Select(frame => frame.MessageId),
                Is.EqualTo(new[] { 0, 0 }));
            Assert.That(server.SentFrames.Select(frame => frame.PayloadValue),
                Is.EqualTo(new[] { 10, 20 }));
        });
    }

    [Test]
    public void Response_WithNullPayload_ReturnsInvalidMessage()
    {
        var handler = new RecordingUnreliableServerHandler();
        TestRRUnreliableServerTransport server = StartServer(handler, NewClock());
        server.Receive(NewClientRoute(), RequestFrame(10));
        IRRUnreliableResponse handle = handler.LastHandle!;

        Assert.That(handle.Response(null!), Is.EqualTo(SendResult.InvalidMessage));

        Assert.Multiple(() =>
        {
            Assert.That(server.SendCalls, Is.Zero);
            Assert.That(handle.Response(TestMessages.Int(1)), Is.EqualTo(SendResult.Ok));
        });
    }

    [Test]
    public void Response_WithOversizedPayload_ReturnsMessageTooBigAndReleases()
    {
        var handler = new RecordingUnreliableServerHandler();
        TestRRUnreliableServerTransport server = StartServer(handler, NewClock());
        server.Receive(NewClientRoute(), RequestFrame(10));
        IRRUnreliableResponse handle = handler.LastHandle!;
        UnionDataList payload = TestMessages.PayloadOfSize(server.MessageLimit + 1);

        Assert.That(handle.Response(payload), Is.EqualTo(SendResult.MessageTooBig));

        Assert.Multiple(() =>
        {
            Assert.That(payload.RefCount.RefCount, Is.Zero);
            Assert.That(server.SendCalls, Is.Zero);
        });
    }

    [Test]
    public void Response_DeclaredSequenceSendsZeroBasedIds()
    {
        var handler = new RecordingUnreliableServerHandler();
        TestRRUnreliableServerTransport server = StartServer(handler, NewClock());
        server.Receive(NewClientRoute(), RequestFrame(10));
        IRRUnreliableResponse handle = handler.LastHandle!;
        handle.SetMessageCount(3);

        for (int value = 1; value <= 3; value++)
        {
            Assert.That(handle.Response(TestMessages.Int(value)), Is.EqualTo(SendResult.Ok));
        }

        Assert.Multiple(() =>
        {
            Assert.That(server.SentFrames.Select(frame => frame.MessageId),
                Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(server.SentFrames.Select(frame => frame.MessageCount),
                Is.EqualTo(new[] { 3, 3, 3 }));
        });
    }

    [Test]
    public void Response_AfterLastMessage_ReturnsError()
    {
        var handler = new RecordingUnreliableServerHandler();
        TestRRUnreliableServerTransport server = StartServer(handler, NewClock());
        server.Receive(NewClientRoute(), RequestFrame(10));
        IRRUnreliableResponse handle = handler.LastHandle!;
        Assert.That(handle.Response(TestMessages.Int(1)), Is.EqualTo(SendResult.Ok));
        UnionDataList payload = TestMessages.Int(2);

        Assert.That(handle.Response(payload), Is.EqualTo(SendResult.Error));

        Assert.Multiple(() =>
        {
            Assert.That(payload.RefCount.RefCount, Is.Zero);
            Assert.That(server.SendCalls, Is.EqualTo(1));
        });
    }

    [Test]
    public void Response_AfterResponseTtlExpires_ReturnsError()
    {
        ManualClock clock = NewClock();
        var handler = new RecordingUnreliableServerHandler();
        TestRRUnreliableServerTransport server = StartServer(handler, clock);
        server.Receive(NewClientRoute(), RequestFrame(10));
        IRRUnreliableResponse handle = handler.LastHandle!;

        clock.Now += ResponseTtl + TimeSpan.FromTicks(1);
        UnionDataList payload = TestMessages.Int(5);

        Assert.That(handle.Response(payload), Is.EqualTo(SendResult.Error));

        Assert.Multiple(() =>
        {
            Assert.That(payload.RefCount.RefCount, Is.Zero);
            Assert.That(server.SendCalls, Is.Zero);
        });
    }

    [Test]
    public void Response_AfterTransportStopped_ReturnsError()
    {
        var handler = new RecordingUnreliableServerHandler();
        TestRRUnreliableServerTransport server = StartServer(handler, NewClock());
        server.Receive(NewClientRoute(), RequestFrame(10));
        IRRUnreliableResponse handle = handler.LastHandle!;
        Assert.That(server.Stop(), Is.True);
        UnionDataList payload = TestMessages.Int(5);

        Assert.That(handle.Response(payload), Is.EqualTo(SendResult.Error));

        Assert.Multiple(() =>
        {
            Assert.That(payload.RefCount.RefCount, Is.Zero);
            Assert.That(server.SendCalls, Is.Zero);
        });
    }

    [Test]
    public void OnFrameReceived_AfterStop_ReleasesWithoutPresenting()
    {
        var handler = new RecordingUnreliableServerHandler();
        TestRRUnreliableServerTransport server = StartServer(handler, NewClock());
        Assert.That(server.Stop(), Is.True);
        UnionDataList frame = RequestFrame(1);

        server.Receive(NewClientRoute(), frame);

        Assert.Multiple(() =>
        {
            Assert.That(frame.RefCount.RefCount, Is.Zero);
            Assert.That(handler.RequestCount, Is.Zero);
        });
    }

    [Test]
    public void OnFrameReceived_DuplicateClientRequest_IsPresentedAgain()
    {
        var handler = new RecordingUnreliableServerHandler();
        TestRRUnreliableServerTransport server = StartServer(handler, NewClock());
        StringEndPoint client = NewClientRoute();

        server.Receive(client, RequestFrame(5, 1));
        server.Receive(client, RequestFrame(5, 2));

        Assert.Multiple(() =>
        {
            Assert.That(handler.RequestCount, Is.EqualTo(2));
            Assert.That(handler.RequestValues, Is.EqualTo(new[] { 1, 2 }));
            Assert.That(handler.Handles[0], Is.Not.SameAs(handler.Handles[1]));
            Assert.That(handler.Handles[0].RequestId, Is.Not.EqualTo(handler.Handles[1].RequestId));
        });
    }

    [Test]
    public void OnFrameReceived_FromDifferentClients_HaveIndependentHandles()
    {
        var handler = new RecordingUnreliableServerHandler();
        TestRRUnreliableServerTransport server = StartServer(handler, NewClock());
        StringEndPoint firstClient = NewClientRoute("rr-unreliable-client-a");
        StringEndPoint secondClient = NewClientRoute("rr-unreliable-client-b");

        server.Receive(firstClient, RequestFrame(5, 1));
        server.Receive(secondClient, RequestFrame(5, 2));
        IRRUnreliableResponse first = handler.Handles[0];
        IRRUnreliableResponse second = handler.Handles[1];

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.Not.SameAs(second));
            Assert.That(first.Response(TestMessages.Int(10)), Is.EqualTo(SendResult.Ok));
            Assert.That(second.Response(TestMessages.Int(20)), Is.EqualTo(SendResult.Ok));
            Assert.That(server.SentFrames[0].Destination, Is.SameAs(firstClient));
            Assert.That(server.SentFrames[1].Destination, Is.SameAs(secondClient));
        });
    }

    [Test]
    [Category("Transports.Concurrency")]
    public void Response_ConcurrentCallsProduceOneSequence()
    {
        var handler = new RecordingUnreliableServerHandler();
        TestRRUnreliableServerTransport server = StartServer(handler, NewClock());
        server.Receive(NewClientRoute(), RequestFrame(10));
        IRRUnreliableResponse handle = handler.LastHandle!;
        handle.SetMessageCount(2);
        server.BlockFirstSend = true;
        SendResult firstResult = SendResult.Unknown;
        SendResult secondResult = SendResult.Unknown;

        var workerA = new Thread(() => firstResult = handle.Response(TestMessages.Int(10)));
        var workerB = new Thread(() => secondResult = handle.Response(TestMessages.Int(20)));
        workerA.Start();

        try
        {
            Assert.That(server.FirstSendEntered.Wait(Timeout), Is.True,
                "the first send must enter before the second worker starts");
            workerB.Start();

            bool bWaitSleepJoin = false;
            bool bBlocked = SpinWait.SpinUntil(
                () => bWaitSleepJoin = (workerB.ThreadState & ThreadState.WaitSleepJoin) != 0,
                Timeout);
            Assert.That(bBlocked, Is.True,
                "worker B must be blocked on the response handle gate");
            Assert.That(bWaitSleepJoin, Is.True);
            Assert.That(server.ActiveSends, Is.EqualTo(1),
                "only the first send may be active while the second worker is blocked");
        }
        finally
        {
            server.ReleaseFirstSend.Set();
        }

        Assert.Multiple(() =>
        {
            Assert.That(workerA.Join(Timeout), Is.True);
            Assert.That(workerB.Join(Timeout), Is.True);
        });

        Assert.Multiple(() =>
        {
            Assert.That(firstResult, Is.EqualTo(SendResult.Ok));
            Assert.That(secondResult, Is.EqualTo(SendResult.Ok));
            Assert.That(server.SentFrames.Select(frame => frame.MessageId),
                Is.EqualTo(new[] { 0, 1 }));
            Assert.That(server.SentFrames.Select(frame => frame.PayloadValue),
                Is.EqualTo(new[] { 10, 20 }));
            Assert.That(server.MaxActiveSends, Is.EqualTo(1));
        });
    }

    private static ManualClock NewClock()
    {
        return new ManualClock { Now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) };
    }

    private static StringEndPoint NewClientRoute(string name = "rr-unreliable-client")
    {
        return TestRRUnreliableServerTransport.NewClientRoute(name);
    }

    private TestRRUnreliableServerTransport StartServer(RecordingUnreliableServerHandler handler,
        ManualClock clock, int messageLimit = MessageLimit, ILogger? logger = null)
    {
        var server = new TestRRUnreliableServerTransport(ResponseTtl, clock, logger, messageLimit);
        Assert.That(server.Init(handler), Is.True);
        Assert.That(server.Start(_ => { }), Is.True);
        _servers.Add(server);
        return server;
    }

    private static UnionDataList RequestFrame(ushort clientRequestId, int value = 1)
    {
        UnionDataList frame = TestMessages.Int(value);
        RRUnreliableFrame.EncodeRequest(frame, clientRequestId);
        return frame;
    }
}
