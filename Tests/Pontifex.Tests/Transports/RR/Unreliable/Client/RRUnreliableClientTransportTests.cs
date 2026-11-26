using System;
using System.Collections.Generic;
using System.Threading;
using Pontifex.Endpoints;
using Pontifex.RR.Unreliable;
using Pontifex.Utils;

namespace Pontifex.Tests.Transports;

[TestFixture]
[Category("Transports")]
[Category("Transports.RR")]
[Category("Transports.RR.Unreliable")]
[Category("Transports.RR.Unreliable.Client")]
public sealed class RRUnreliableClientTransportTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan RequestTtl = TimeSpan.FromSeconds(3);
    private const int MessageLimit = 64;

    private readonly List<TestRRUnreliableClientTransport> _clients = new List<TestRRUnreliableClientTransport>();

    [TearDown]
    public void TearDown()
    {
        foreach (TestRRUnreliableClientTransport client in _clients)
        {
            client.Stop();
        }

        _clients.Clear();
    }

    [Test]
    public void Constructor_WithNullServerEndPoint_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new TestRRUnreliableClientTransport(null, RequestTtl, NewClock()));
    }

    [Test]
    public void Constructor_WithNonPositiveRequestTtl_Throws()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new TestRRUnreliableClientTransport(NewServerRoute(), TimeSpan.Zero, NewClock()));

        Assert.That(exception!.ParamName, Is.EqualTo("ttl"));
    }

    [Test]
    public void Constructor_ExposesServerEndPointAndRequestTtl()
    {
        IEndPoint route = NewServerRoute();
        var transport = new TestRRUnreliableClientTransport(route, RequestTtl, NewClock());

        Assert.Multiple(() =>
        {
            Assert.That(transport.ServerEndPoint, Is.SameAs(route));
            Assert.That(transport.RequestTtl, Is.EqualTo(RequestTtl));
        });
    }

    [Test]
    public void Request_WithNullHandler_ThrowsBeforeConsumingPayload()
    {
        TestRRUnreliableClientTransport client = StartClient(NewClock());
        UnionDataList payload = TestMessages.Int(1);

        Assert.Throws<ArgumentNullException>(() => client.Request(payload, null!));

        Assert.That(payload.IsAlive, Is.True, "the handler check must run before the payload is consumed");
        payload.Release();
    }

    [Test]
    public void Request_WithNonRRHandler_ThrowsBeforeConsumingPayload()
    {
        TestRRUnreliableClientTransport client = StartClient(NewClock());
        var handler = new PlainUnreliableClientHandler();
        UnionDataList payload = TestMessages.Int(1);

        Assert.Throws<ArgumentException>(() => client.Request(payload, handler));

        Assert.That(payload.IsAlive, Is.True, "the handler check must run before the payload is consumed");
        payload.Release();
    }

    [Test]
    public void Request_WithAlreadyBoundHandler_Throws()
    {
        TestRRUnreliableClientTransport client = StartClient(NewClock());
        var handler = new RecordingUnreliableClientHandler();
        Assert.That(client.Request(TestMessages.Int(1), handler), Is.EqualTo(SendResult.Ok));
        UnionDataList second = TestMessages.Int(2);

        Assert.Throws<InvalidOperationException>(() => client.Request(second, handler));

        Assert.That(second.IsAlive, Is.True, "the bound-handler check must run before the payload is consumed");
        second.Release();
    }

    [Test]
    public void Request_AfterStop_ReturnsNotConnectedAndReleases()
    {
        TestRRUnreliableClientTransport client = StartClient(NewClock());
        Assert.That(client.Stop(), Is.True);
        var handler = new RecordingUnreliableClientHandler();
        UnionDataList payload = TestMessages.Int(1);

        Assert.That(client.Request(payload, handler), Is.EqualTo(SendResult.NotConnected));

        Assert.Multiple(() =>
        {
            Assert.That(payload.RefCount.RefCount, Is.Zero);
            Assert.That(handler.IsBound, Is.False);
            Assert.That(client.SendCalls, Is.Zero);
        });
    }

    [Test]
    public void Request_WithNullPayload_ReturnsInvalidMessage()
    {
        TestRRUnreliableClientTransport client = StartClient(NewClock());
        var handler = new RecordingUnreliableClientHandler();

        Assert.That(client.Request(null!, handler), Is.EqualTo(SendResult.InvalidMessage));

        Assert.That(handler.IsBound, Is.False);
    }

    [Test]
    public void Request_AboveMessageLimit_ReturnsMessageTooBigAndReleases()
    {
        TestRRUnreliableClientTransport client = StartClient(NewClock());
        var handler = new RecordingUnreliableClientHandler();
        UnionDataList payload = TestMessages.PayloadOfSize(client.MessageLimit + 1);

        Assert.That(client.Request(payload, handler), Is.EqualTo(SendResult.MessageTooBig));

        Assert.Multiple(() =>
        {
            Assert.That(payload.RefCount.RefCount, Is.Zero);
            Assert.That(handler.IsBound, Is.False);
            Assert.That(client.PendingCount, Is.Zero);
        });
    }

    [Test]
    public void Request_WhenHandlerCancelsBeforePublish_ReturnsErrorAndReleases()
    {
        TestRRUnreliableClientTransport client = StartClient(NewClock());
        var handler = new RecordingUnreliableClientHandler();
        client.AfterAddBeforeBind = _ => handler.Cancel();
        UnionDataList payload = TestMessages.Int(1);

        Assert.That(client.Request(payload, handler), Is.EqualTo(SendResult.Error));

        Assert.Multiple(() =>
        {
            Assert.That(payload.RefCount.RefCount, Is.Zero);
            Assert.That(handler.IsBound, Is.False);
            Assert.That(client.PendingCount, Is.Zero);
        });
    }

    [Test]
    public void Request_WhenStopRacesBeforeSend_ReturnsErrorAndUnbinds()
    {
        TestRRUnreliableClientTransport client = StartClient(NewClock());
        var handler = new RecordingUnreliableClientHandler();
        client.OnSend = () => client.Stop();

        Assert.That(client.Request(TestMessages.Int(1), handler), Is.EqualTo(SendResult.Error));

        Assert.Multiple(() =>
        {
            Assert.That(client.SendCalls, Is.EqualTo(1));
            Assert.That(client.IsStarted, Is.False);
            Assert.That(handler.IsBound, Is.False);
            Assert.That(client.PendingCount, Is.Zero);
        });
    }

    [Test]
    public void Request_WhenSendFrameReturnsNonOk_ReturnsResultAndUnbindsWithoutCallback()
    {
        TestRRUnreliableClientTransport client = StartClient(NewClock());
        client.SendResult = SendResult.BufferOverflow;
        var handler = new RecordingUnreliableClientHandler();

        Assert.That(client.Request(TestMessages.Int(1), handler), Is.EqualTo(SendResult.BufferOverflow));
        ushort requestId = handler.RequestId!.Value;
        UnionDataList response = ResponseFrame(requestId, 0, 1);
        client.Receive(response);

        Assert.Multiple(() =>
        {
            Assert.That(handler.IsBound, Is.False);
            Assert.That(handler.ResponseCount, Is.Zero);
            Assert.That(client.PendingCount, Is.Zero);
            Assert.That(response.RefCount.RefCount, Is.Zero);
        });
    }

    [Test]
    public void Request_WhenSendFrameThrows_ReturnsErrorAndUnbindsWithoutCallback()
    {
        TestRRUnreliableClientTransport client = StartClient(NewClock());
        client.ThrowOnSend = true;
        var handler = new RecordingUnreliableClientHandler();

        Assert.That(client.Request(TestMessages.Int(1), handler), Is.EqualTo(SendResult.Error));
        ushort requestId = handler.RequestId!.Value;
        UnionDataList response = ResponseFrame(requestId, 0, 1);
        client.Receive(response);

        Assert.Multiple(() =>
        {
            Assert.That(handler.IsBound, Is.False);
            Assert.That(handler.ResponseCount, Is.Zero);
            Assert.That(client.PendingCount, Is.Zero);
            Assert.That(response.RefCount.RefCount, Is.Zero);
        });
    }

    [Test]
    public void RequestId_IsPublishedBeforeSendFrame()
    {
        TestRRUnreliableClientTransport client = StartClient(NewClock());
        var handler = new RecordingUnreliableClientHandler();
        ushort? publishedBeforeSend = null;
        client.OnSend = () => publishedBeforeSend = handler.RequestId;

        Assert.That(client.Request(TestMessages.Int(1), handler), Is.EqualTo(SendResult.Ok));

        Assert.Multiple(() =>
        {
            Assert.That(publishedBeforeSend, Is.Not.Null);
            Assert.That(publishedBeforeSend, Is.EqualTo(handler.RequestId));
        });
    }

    [Test]
    public void Response_WhenDeliveredBeforeRequestReturns_IsStillDelivered()
    {
        TestRRUnreliableClientTransport client = StartClient(NewClock());
        var handler = new RecordingUnreliableClientHandler();
        client.OnSend = () => client.Receive(ResponseFrame(handler.RequestId!.Value, 0, 1, 7));

        Assert.That(client.Request(TestMessages.Int(1), handler), Is.EqualTo(SendResult.Ok));

        Assert.Multiple(() =>
        {
            Assert.That(handler.ResponseCount, Is.EqualTo(1));
            Assert.That(handler.ResponseValues, Is.EqualTo(new[] { 7 }));
            Assert.That(handler.IsBound, Is.False);
        });
    }

    [Test]
    public void Response_FromForeignRoute_IsReleasedWithoutCallback()
    {
        TestRRUnreliableClientTransport client = StartClient(NewClock());
        var handler = new RecordingUnreliableClientHandler();
        Assert.That(client.Request(TestMessages.Int(1), handler), Is.EqualTo(SendResult.Ok));
        UnionDataList response = ResponseFrame(handler.RequestId!.Value, 0, 1);

        client.ReceiveFrom(new StringEndPoint("foreign-route"), response);

        Assert.Multiple(() =>
        {
            Assert.That(response.RefCount.RefCount, Is.Zero);
            Assert.That(handler.ResponseCount, Is.Zero);
            Assert.That(client.PendingCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void Response_WhenMalformed_IsReleasedWithoutCallback()
    {
        TestRRUnreliableClientTransport client = StartClient(NewClock());
        var handler = new RecordingUnreliableClientHandler();
        Assert.That(client.Request(TestMessages.Int(1), handler), Is.EqualTo(SendResult.Ok));
        UnionDataList response = TestMessages.Int(7);

        client.Receive(response);

        Assert.Multiple(() =>
        {
            Assert.That(response.RefCount.RefCount, Is.Zero);
            Assert.That(handler.ResponseCount, Is.Zero);
        });
    }

    [Test]
    public void Response_WhenOversized_IsReleasedWithoutCallback()
    {
        TestRRUnreliableClientTransport client = StartClient(NewClock());
        var handler = new RecordingUnreliableClientHandler();
        Assert.That(client.Request(TestMessages.Int(1), handler), Is.EqualTo(SendResult.Ok));
        UnionDataList response = TestMessages.PayloadOfSize(client.MessageLimit + 1);
        RRUnreliableFrame.EncodeResponse(response, handler.RequestId!.Value, 0, 1);

        client.Receive(response);

        Assert.Multiple(() =>
        {
            Assert.That(response.RefCount.RefCount, Is.Zero);
            Assert.That(handler.ResponseCount, Is.Zero);
        });
    }

    [Test]
    public void Response_ForUnknownId_IsReleasedWithoutCallback()
    {
        TestRRUnreliableClientTransport client = StartClient(NewClock());
        var handler = new RecordingUnreliableClientHandler();
        Assert.That(client.Request(TestMessages.Int(1), handler), Is.EqualTo(SendResult.Ok));
        ushort unknownId = unchecked((ushort)(handler.RequestId!.Value + 1));
        UnionDataList response = ResponseFrame(unknownId, 0, 1);

        client.Receive(response);

        Assert.Multiple(() =>
        {
            Assert.That(response.RefCount.RefCount, Is.Zero);
            Assert.That(handler.ResponseCount, Is.Zero);
            Assert.That(client.PendingCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void Response_AfterRequestTtlExpires_IsDroppedAndHandlerUnbound()
    {
        var clock = NewClock();
        TestRRUnreliableClientTransport client = StartClient(clock, RequestTtl);
        var handler = new RecordingUnreliableClientHandler();
        Assert.That(client.Request(TestMessages.Int(1), handler), Is.EqualTo(SendResult.Ok));
        UnionDataList response = ResponseFrame(handler.RequestId!.Value, 0, 1);

        clock.Now += RequestTtl;

        client.Receive(response);

        Assert.Multiple(() =>
        {
            Assert.That(response.RefCount.RefCount, Is.Zero);
            Assert.That(handler.ResponseCount, Is.Zero);
            Assert.That(handler.IsBound, Is.False);
            Assert.That(client.PendingCount, Is.Zero);
        });
    }

    [Test]
    public void Response_FirstFrameFixesMessageCount()
    {
        TestRRUnreliableClientTransport client = StartClient(NewClock());
        var handler = new RecordingUnreliableClientHandler();
        Assert.That(client.Request(TestMessages.Int(1), handler), Is.EqualTo(SendResult.Ok));
        ushort requestId = handler.RequestId!.Value;

        client.Receive(ResponseFrame(requestId, 0, 2, 10));
        client.Receive(ResponseFrame(requestId, 1, 2, 20));

        Assert.Multiple(() =>
        {
            Assert.That(handler.MessageIds, Is.EqualTo(new[] { 0, 1 }));
            Assert.That(handler.MessageCounts, Is.EqualTo(new[] { 2, 2 }));
            Assert.That(handler.ResponseValues, Is.EqualTo(new[] { 10, 20 }));
            Assert.That(handler.IsBound, Is.False);
            Assert.That(client.PendingCount, Is.Zero);
        });
    }

    [Test]
    public void Response_WithInconsistentMessageCount_IsDropped()
    {
        TestRRUnreliableClientTransport client = StartClient(NewClock());
        var handler = new RecordingUnreliableClientHandler();
        Assert.That(client.Request(TestMessages.Int(1), handler), Is.EqualTo(SendResult.Ok));
        ushort requestId = handler.RequestId!.Value;
        client.Receive(ResponseFrame(requestId, 0, 2, 10));
        UnionDataList inconsistent = ResponseFrame(requestId, 1, 3, 20);

        client.Receive(inconsistent);

        Assert.Multiple(() =>
        {
            Assert.That(inconsistent.RefCount.RefCount, Is.Zero);
            Assert.That(handler.ResponseCount, Is.EqualTo(1));
        });

        client.Receive(ResponseFrame(requestId, 1, 2, 30));

        Assert.Multiple(() =>
        {
            Assert.That(handler.MessageIds, Is.EqualTo(new[] { 0, 1 }));
            Assert.That(handler.MessageCounts, Is.EqualTo(new[] { 2, 2 }));
        });
    }

    [Test]
    public void Response_WithMessageIdAtOrAboveCount_IsDropped()
    {
        TestRRUnreliableClientTransport client = StartClient(NewClock());
        var handler = new RecordingUnreliableClientHandler();
        Assert.That(client.Request(TestMessages.Int(1), handler), Is.EqualTo(SendResult.Ok));
        ushort requestId = handler.RequestId!.Value;

        for (int messageId = 2; messageId <= 3; messageId++)
        {
            UnionDataList invalid = ManualResponseFrame(requestId, messageId, 2);

            client.Receive(invalid);

            Assert.That(invalid.RefCount.RefCount, Is.Zero,
                $"the frame with message id {messageId} must be released");
        }

        Assert.Multiple(() =>
        {
            Assert.That(handler.ResponseCount, Is.Zero);
            Assert.That(client.PendingCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void Response_WithDuplicateMessageId_IsDropped()
    {
        TestRRUnreliableClientTransport client = StartClient(NewClock());
        var handler = new RecordingUnreliableClientHandler();
        Assert.That(client.Request(TestMessages.Int(1), handler), Is.EqualTo(SendResult.Ok));
        ushort requestId = handler.RequestId!.Value;
        client.Receive(ResponseFrame(requestId, 0, 2, 10));
        UnionDataList duplicate = ResponseFrame(requestId, 0, 2, 20);

        client.Receive(duplicate);

        Assert.Multiple(() =>
        {
            Assert.That(duplicate.RefCount.RefCount, Is.Zero);
            Assert.That(handler.ResponseCount, Is.EqualTo(1));
        });

        client.Receive(ResponseFrame(requestId, 1, 2, 30));

        Assert.That(handler.MessageIds, Is.EqualTo(new[] { 0, 1 }));
    }

    [Test]
    public void Response_LastMessage_UnbindsHandlerAfterCallback()
    {
        TestRRUnreliableClientTransport client = StartClient(NewClock());
        var handler = new RecordingUnreliableClientHandler();
        Assert.That(client.Request(TestMessages.Int(1), handler), Is.EqualTo(SendResult.Ok));

        client.Receive(ResponseFrame(handler.RequestId!.Value, 0, 1, 10));

        Assert.Multiple(() =>
        {
            Assert.That(handler.ResponseCount, Is.EqualTo(1));
            Assert.That(handler.IsBound, Is.False);
            Assert.That(client.PendingCount, Is.Zero);
        });

        Assert.That(client.Request(TestMessages.Int(2), handler), Is.EqualTo(SendResult.Ok));
    }

    [Test]
    public void Response_WhenHandlerThrows_IsIsolatedAndStillCompletes()
    {
        TestRRUnreliableClientTransport client = StartClient(NewClock());
        var handler = new RecordingUnreliableClientHandler { ThrowOnResponse = true };
        Assert.That(client.Request(TestMessages.Int(1), handler), Is.EqualTo(SendResult.Ok));

        Assert.DoesNotThrow(() =>
            client.Receive(ResponseFrame(handler.RequestId!.Value, 0, 1, 10)));

        Assert.Multiple(() =>
        {
            Assert.That(client.IsStarted, Is.True);
            Assert.That(client.IsValid, Is.True);
            Assert.That(handler.IsBound, Is.False);
            Assert.That(client.PendingCount, Is.Zero);
        });

        Assert.That(client.Request(TestMessages.Int(2), handler), Is.EqualTo(SendResult.Ok));
    }

    [Test]
    [Timeout(5000)]
    [Category("Transports.Concurrency")]
    public void Response_Callbacks_AreSerializedPerRequest()
    {
        TestRRUnreliableClientTransport client = StartClient(NewClock());
        var handler = new RecordingUnreliableClientHandler { BlockFirstCallback = true };
        Assert.That(client.Request(TestMessages.Int(1), handler), Is.EqualTo(SendResult.Ok));
        ushort requestId = handler.RequestId!.Value;

        var workerA = new Thread(() => client.Receive(ResponseFrame(requestId, 0, 2, 10)));
        var workerB = new Thread(() => client.Receive(ResponseFrame(requestId, 1, 2, 20)));
        workerA.Start();

        try
        {
            Assert.That(handler.Entered.Wait(Timeout), Is.True,
                "the first callback must enter before the second worker starts");
            workerB.Start();

            bool bWaitSleepJoin = false;
            bool bBlocked = SpinWait.SpinUntil(
                () => bWaitSleepJoin = (workerB.ThreadState & ThreadState.WaitSleepJoin) != 0,
                Timeout);
            Assert.That(bBlocked, Is.True,
                "worker B must be blocked on the per-request callback gate");
            Assert.That(bWaitSleepJoin, Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(handler.ActiveCallbacks, Is.EqualTo(1),
                    "only the first callback may be active while the second worker is blocked");
                Assert.That(handler.ResponseCount, Is.EqualTo(1),
                    "the second callback must not have entered");
            });
        }
        finally
        {
            handler.Release.Set();
        }

        Assert.Multiple(() =>
        {
            Assert.That(workerA.Join(Timeout), Is.True);
            Assert.That(workerB.Join(Timeout), Is.True);
        });

        Assert.Multiple(() =>
        {
            Assert.That(handler.MessageIds, Is.EqualTo(new[] { 0, 1 }));
            Assert.That(handler.ResponseValues, Is.EqualTo(new[] { 10, 20 }));
            Assert.That(handler.MaxActiveCallbacks, Is.EqualTo(1));
        });
    }

    [Test]
    [Timeout(5000)]
    [Category("Transports.Concurrency")]
    public void Stop_FromInsideAResponseCallback_DoesNotDeadlock()
    {
        TestRRUnreliableClientTransport client = StartClient(NewClock());
        var handler = new RecordingUnreliableClientHandler();
        bool stopCompleted = false;
        bool stopResult = false;
        handler.OnResponse = () =>
        {
            stopResult = client.Stop();
            stopCompleted = true;
        };
        Assert.That(client.Request(TestMessages.Int(1), handler), Is.EqualTo(SendResult.Ok));

        Assert.DoesNotThrow(() =>
            client.Receive(ResponseFrame(handler.RequestId!.Value, 0, 1, 10)));

        Assert.Multiple(() =>
        {
            Assert.That(stopCompleted, Is.True, "Stop must return from inside the callback");
            Assert.That(stopResult, Is.True);
            Assert.That(client.IsStarted, Is.False);
            Assert.That(handler.IsBound, Is.False);
            Assert.That(client.PendingCount, Is.Zero);
        });
    }

    [Test]
    [Timeout(5000)]
    [Category("Transports.Concurrency")]
    public void Request_FromInsideAResponseCallback_IsAccepted()
    {
        TestRRUnreliableClientTransport client = StartClient(NewClock());
        var outer = new RecordingUnreliableClientHandler();
        var nested = new RecordingUnreliableClientHandler();
        SendResult nestedResult = SendResult.Unknown;
        outer.OnResponse = () =>
            nestedResult = client.Request(TestMessages.Int(2), nested);
        Assert.That(client.Request(TestMessages.Int(1), outer), Is.EqualTo(SendResult.Ok));

        Assert.DoesNotThrow(() =>
            client.Receive(ResponseFrame(outer.RequestId!.Value, 0, 1, 10)));

        Assert.Multiple(() =>
        {
            Assert.That(nestedResult, Is.EqualTo(SendResult.Ok));
            Assert.That(nested.RequestId, Is.Not.Null);
            Assert.That(client.PendingCount, Is.EqualTo(1));
        });

        nested.Cancel();

        Assert.That(client.PendingCount, Is.Zero);
    }

    [Test]
    public void Cancel_RemovesPendingRequestWithoutCallback()
    {
        TestRRUnreliableClientTransport client = StartClient(NewClock());
        var handler = new RecordingUnreliableClientHandler();
        Assert.That(client.Request(TestMessages.Int(1), handler), Is.EqualTo(SendResult.Ok));
        ushort requestId = handler.RequestId!.Value;

        handler.Cancel();

        Assert.Multiple(() =>
        {
            Assert.That(client.PendingCount, Is.Zero);
            Assert.That(handler.IsBound, Is.False);
        });

        UnionDataList response = ResponseFrame(requestId, 0, 1);
        client.Receive(response);

        Assert.Multiple(() =>
        {
            Assert.That(response.RefCount.RefCount, Is.Zero);
            Assert.That(handler.ResponseCount, Is.Zero);
        });
    }

    [Test]
    public void Cancel_BeforeSend_RemovesPendingEntryAndDropsLaterResponse()
    {
        TestRRUnreliableClientTransport client = StartClient(NewClock());
        var handler = new RecordingUnreliableClientHandler();
        client.BeforeSend = () => handler.Cancel();

        Assert.That(client.Request(TestMessages.Int(1), handler), Is.EqualTo(SendResult.Ok));

        Assert.Multiple(() =>
        {
            Assert.That(client.PendingCount, Is.Zero);
            Assert.That(handler.IsBound, Is.False);
        });

        UnionDataList response = ResponseFrame(handler.RequestId!.Value, 0, 1);
        client.Receive(response);

        Assert.Multiple(() =>
        {
            Assert.That(response.RefCount.RefCount, Is.Zero);
            Assert.That(handler.ResponseCount, Is.Zero);
        });
    }

    [Test]
    public void Cancel_InsideANonLastMessageCallback_DefersUnbind()
    {
        TestRRUnreliableClientTransport client = StartClient(NewClock());
        var handler = new RecordingUnreliableClientHandler { CancelFromCallback = true };
        Assert.That(client.Request(TestMessages.Int(1), handler), Is.EqualTo(SendResult.Ok));

        client.Receive(ResponseFrame(handler.RequestId!.Value, 0, 2, 10));

        Assert.Multiple(() =>
        {
            Assert.That(handler.BoundAfterCancel, Is.True,
                "the handler must stay bound while the callback runs");
            Assert.That(handler.IsBound, Is.False);
            Assert.That(handler.ResponseCount, Is.EqualTo(1));
            Assert.That(client.PendingCount, Is.Zero);
        });
    }

    [Test]
    public void Stop_ClearsPendingWindowWithoutCallbacks()
    {
        TestRRUnreliableClientTransport client = StartClient(NewClock());
        var first = new RecordingUnreliableClientHandler();
        var second = new RecordingUnreliableClientHandler();
        Assert.That(client.Request(TestMessages.Int(1), first), Is.EqualTo(SendResult.Ok));
        Assert.That(client.Request(TestMessages.Int(2), second), Is.EqualTo(SendResult.Ok));

        Assert.That(client.Stop(), Is.True);

        Assert.Multiple(() =>
        {
            Assert.That(client.PendingCount, Is.Zero);
            Assert.That(first.IsBound, Is.False);
            Assert.That(second.IsBound, Is.False);
            Assert.That(first.ResponseCount, Is.Zero);
            Assert.That(second.ResponseCount, Is.Zero);
        });
    }

    [Test]
    public void Response_WhenTransportStopped_IsDropped()
    {
        TestRRUnreliableClientTransport client = StartClient(NewClock());
        var handler = new RecordingUnreliableClientHandler();
        Assert.That(client.Request(TestMessages.Int(1), handler), Is.EqualTo(SendResult.Ok));
        ushort requestId = handler.RequestId!.Value;
        Assert.That(client.Stop(), Is.True);
        UnionDataList response = ResponseFrame(requestId, 0, 1);

        client.Receive(response);

        Assert.Multiple(() =>
        {
            Assert.That(response.RefCount.RefCount, Is.Zero);
            Assert.That(handler.ResponseCount, Is.Zero);
        });
    }

    [Test]
    public void ExpiredRequest_AllowsTheSameHandlerToBeReused()
    {
        var clock = NewClock();
        TestRRUnreliableClientTransport client = StartClient(clock, RequestTtl);
        var handler = new RecordingUnreliableClientHandler();
        Assert.That(client.Request(TestMessages.Int(1), handler), Is.EqualTo(SendResult.Ok));
        ushort firstId = handler.RequestId!.Value;

        clock.Now += RequestTtl;

        Assert.That(client.Request(TestMessages.Int(2), handler), Is.EqualTo(SendResult.Ok));

        Assert.Multiple(() =>
        {
            Assert.That(client.PendingCount, Is.EqualTo(1));
            Assert.That(handler.IsBound, Is.True);
            Assert.That(handler.RequestId, Is.Not.EqualTo(firstId));
        });
    }

    private static ManualClock NewClock()
    {
        return new ManualClock { Now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) };
    }

    private static StringEndPoint NewServerRoute()
    {
        return new StringEndPoint("rr-unreliable-client-server");
    }

    private TestRRUnreliableClientTransport StartClient(ManualClock clock,
        TimeSpan? requestTtl = null, int messageLimit = MessageLimit)
    {
        var client = new TestRRUnreliableClientTransport(NewServerRoute(),
            requestTtl ?? RequestTtl, clock, messageLimit);
        Assert.That(client.Start(_ => { }), Is.True);
        _clients.Add(client);
        return client;
    }

    private static UnionDataList ResponseFrame(ushort requestId, int messageId, int messageCount,
        int value = 1)
    {
        UnionDataList frame = TestMessages.Int(value);
        RRUnreliableFrame.EncodeResponse(frame, requestId, messageId, messageCount);
        return frame;
    }

    private static UnionDataList ManualResponseFrame(ushort requestId, int messageId,
        int messageCount, int value = 1)
    {
        UnionDataList frame = TestMessages.Int(value);
        frame.PutFirst(new UnionData((byte)(messageCount - 1)));
        frame.PutFirst(new UnionData((byte)messageId));
        frame.PutFirst(new UnionData(requestId));
        return frame;
    }

    private sealed class PlainUnreliableClientHandler : IRRUnreliableRequestHandler
    {
        public ushort? RequestId => null;

        public void Cancel()
        {
        }

        public void Response(UnionDataList response, int messageId, int messageCount)
        {
            response.Release();
        }
    }
}
