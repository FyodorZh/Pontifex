using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Actuarius.Memory;
using Pontifex.Endpoints;
using Pontifex.RR.Reliable;
using Pontifex.Tests.RR.RRReliableTransport.Harness;
using Pontifex.Tests.RR.RRReliableTransport.Server.Harness;
using Pontifex.Tests.RR.TestSupport;
using Pontifex.Utils;

namespace Pontifex.Tests.RR.RRReliableTransport;

[TestFixture]
[Category("Direct.RRReliableTransport")]
[Category("Direct.RRReliableTransport.Server")]
public sealed class RRReliableServerTransportTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private readonly List<TestReliableServerTransport> _servers = new List<TestReliableServerTransport>();

    [TearDown]
    public void TearDown()
    {
        foreach (TestReliableServerTransport server in _servers)
        {
            server.Stop();
            server.StopDispatchForTests();
        }

        _servers.Clear();
    }

    [Test]
    public void ResponseAndFinish_ProduceOrderedFramesWithServerIdsFromOne()
    {
        using var carrier = new ScriptedReliableCarrier();
        var factory = new ManualTimerFactory();
        var handler = new RecordingReliableServerHandler();
        TestReliableServerTransport server = StartServer(carrier, factory, handler);
        IEndPoint source = carrier.ClientEndPoint;

        server.ReceiveFromClient(source, RequestFrame(1, 5));
        Assert.That(WaitForRequestCount(handler, 1), Is.True);
        IRRReliableResponse first = handler.LastHandle!;

        server.ReceiveFromClient(source, RequestFrame(2, 6));
        Assert.That(WaitForRequestCount(handler, 2), Is.True);
        IRRReliableResponse second = handler.LastHandle!;

        Assert.That(first.RequestId, Is.EqualTo(1));
        Assert.That(second.RequestId, Is.EqualTo(2));

        Assert.That(first.Response(Payload(10)), Is.EqualTo(SendResult.Ok));
        Assert.That(first.Response(Payload(20)), Is.EqualTo(SendResult.Ok));
        Assert.That(first.Finish(), Is.EqualTo(SendResult.Ok));

        Assert.That(carrier.SentByServer.Count, Is.EqualTo(3));
        Assert.That(carrier.SentByServer[0].FrameType, Is.EqualTo(RRReliableFrameType.Response));
        Assert.That(carrier.SentByServer[0].Ordinal, Is.EqualTo(1u));
        Assert.That(carrier.SentByServer[1].Ordinal, Is.EqualTo(2u));
        Assert.That(carrier.SentByServer[2].FrameType, Is.EqualTo(RRReliableFrameType.Finish));
        Assert.That(carrier.SentByServer[2].Completion,
            Is.EqualTo(RRReliableCompletionType.Normal));
    }

    [Test]
    public void ResponseOrdinal_IsCommittedOnlyAfterOk()
    {
        using var carrier = new ScriptedReliableCarrier();
        var factory = new ManualTimerFactory();
        var handler = new RecordingReliableServerHandler();
        TestReliableServerTransport server = StartServer(carrier, factory, handler);
        server.ReceiveFromClient(carrier.ClientEndPoint, RequestFrame(1, 5));
        Assert.That(WaitForRequestCount(handler, 1), Is.True);
        IRRReliableResponse handle = handler.LastHandle!;

        server.SendOverride = (_, frame) =>
        {
            frame.Release();
            return SendResult.Error;
        };

        Assert.That(handle.Response(Payload(10)), Is.EqualTo(SendResult.Error));

        server.SendOverride = null;
        Assert.That(handle.Response(Payload(20)), Is.EqualTo(SendResult.Ok));

        Assert.That(carrier.SentByServer.Count, Is.EqualTo(1));
        Assert.That(carrier.SentByServer[0].Ordinal, Is.EqualTo(1u));
        Assert.That(carrier.SentByServer[0].Payload, Is.EqualTo(new[] { 20 }));
    }

    [Test]
    public void ResponseHandle_MatrixOfFinishRejectAndClosedCalls()
    {
        using var carrier = new ScriptedReliableCarrier();
        var factory = new ManualTimerFactory();
        var handler = new RecordingReliableServerHandler();
        TestReliableServerTransport server = StartServer(carrier, factory, handler);
        server.ReceiveFromClient(carrier.ClientEndPoint, RequestFrame(1, 5));
        Assert.That(WaitForRequestCount(handler, 1), Is.True);
        IRRReliableResponse handle = handler.LastHandle!;

        Assert.That(handle.Response(Payload(10)), Is.EqualTo(SendResult.Ok));
        Assert.Throws<InvalidOperationException>(() => handle.Reject());
        Assert.That(handle.Finish(), Is.EqualTo(SendResult.Ok));
        Assert.That(handle.Finish(), Is.EqualTo(SendResult.Error));
        Assert.That(handle.Response(Payload(11)), Is.EqualTo(SendResult.Error));
        Assert.Throws<InvalidOperationException>(() => handle.Reject());

        server.Stop();

        Assert.That(handle.Response(Payload(12)), Is.EqualTo(SendResult.Error));
        Assert.That(handle.Finish(), Is.EqualTo(SendResult.Error));
        Assert.That(handle.Reject(), Is.EqualTo(SendResult.Error));
    }

    [Test]
    public void Finish_WhenTheSendFails_LeavesTheHandleRetryable()
    {
        using var carrier = new ScriptedReliableCarrier();
        var factory = new ManualTimerFactory();
        var handler = new RecordingReliableServerHandler();
        TestReliableServerTransport server = StartServer(carrier, factory, handler);
        server.ReceiveFromClient(carrier.ClientEndPoint, RequestFrame(1, 5));
        Assert.That(WaitForRequestCount(handler, 1), Is.True);
        IRRReliableResponse handle = handler.LastHandle!;
        carrier.ServerSendOverride = (_, frame) =>
        {
            frame.Release();
            return SendResult.Error;
        };

        Assert.That(handle.Finish(), Is.EqualTo(SendResult.Error));
        carrier.ServerSendOverride = null;

        Assert.That(handle.Finish(), Is.EqualTo(SendResult.Ok));
        Assert.That(handle.Finish(), Is.EqualTo(SendResult.Error));
        server.Stop();
    }

    [Test]
    public void Reject_WhenTheSendFails_LeavesTheHandleRetryable()
    {
        using var carrier = new ScriptedReliableCarrier();
        var factory = new ManualTimerFactory();
        var handler = new RecordingReliableServerHandler();
        TestReliableServerTransport server = StartServer(carrier, factory, handler);
        server.ReceiveFromClient(carrier.ClientEndPoint, RequestFrame(1, 5));
        Assert.That(WaitForRequestCount(handler, 1), Is.True);
        IRRReliableResponse handle = handler.LastHandle!;
        carrier.ServerSendOverride = (_, frame) =>
        {
            frame.Release();
            return SendResult.Error;
        };

        Assert.That(handle.Reject(), Is.EqualTo(SendResult.Error));
        carrier.ServerSendOverride = null;

        Assert.That(handle.Reject(), Is.EqualTo(SendResult.Ok));
        Assert.That(handle.Reject(), Is.EqualTo(SendResult.Ok));
        server.Stop();
    }

    [Test]
    public void Reject_AfterTheResponseTtlClosedTheHandle_ReturnsError()
    {
        using var carrier = new ScriptedReliableCarrier();
        var factory = new ManualTimerFactory();
        var handler = new RecordingReliableServerHandler();
        TestReliableServerTransport server = StartServer(carrier, factory, handler);
        server.ReceiveFromClient(carrier.ClientEndPoint, RequestFrame(1, 5));
        Assert.That(WaitForRequestCount(handler, 1), Is.True);
        IRRReliableResponse handle = handler.LastHandle!;

        Assert.That(factory.FireAll(), Is.GreaterThan(0));

        Assert.Multiple(() =>
        {
            Assert.That(handle.Reject(), Is.EqualTo(SendResult.Error));
            Assert.That(handle.Response(Payload(1)), Is.EqualTo(SendResult.Error));
            Assert.That(handle.Finish(), Is.EqualTo(SendResult.Error));
        });
        server.Stop();
    }

    [Test]
    public void Reject_ClosesTheHandleAndRepeatedRejectReturnsOk()
    {
        using var carrier = new ScriptedReliableCarrier();
        var factory = new ManualTimerFactory();
        var handler = new RecordingReliableServerHandler();
        TestReliableServerTransport server = StartServer(carrier, factory, handler);
        server.ReceiveFromClient(carrier.ClientEndPoint, RequestFrame(1, 5));
        Assert.That(WaitForRequestCount(handler, 1), Is.True);
        IRRReliableResponse handle = handler.LastHandle!;

        Assert.That(handle.Reject(), Is.EqualTo(SendResult.Ok));
        Assert.That(handle.Reject(), Is.EqualTo(SendResult.Ok));
        Assert.Throws<InvalidOperationException>(() => handle.Response(Payload(10)));
        Assert.Throws<InvalidOperationException>(() => handle.Finish());

        Assert.That(carrier.SentByServer.Count, Is.EqualTo(1));
        Assert.That(carrier.SentByServer[0].Completion,
            Is.EqualTo(RRReliableCompletionType.Rejected));
        Assert.That(factory.PendingCount, Is.Zero);
    }

    [Test]
    public void Cancel_ClosesTheLiveHandleAndSuppressesLaterResponses()
    {
        using var carrier = new ScriptedReliableCarrier();
        var factory = new ManualTimerFactory();
        var handler = new RecordingReliableServerHandler();
        TestReliableServerTransport server = StartServer(carrier, factory, handler);
        server.ReceiveFromClient(carrier.ClientEndPoint, RequestFrame(1, 5));
        Assert.That(WaitForRequestCount(handler, 1), Is.True);
        IRRReliableResponse handle = handler.LastHandle!;

        server.ReceiveFromClient(carrier.ClientEndPoint, CancelFrame(1));

        Assert.That(handle.Response(Payload(10)), Is.EqualTo(SendResult.Error));
        Assert.That(handle.Finish(), Is.EqualTo(SendResult.Error));
        Assert.That(carrier.SentByServer.Count, Is.Zero);
        Assert.That(factory.PendingCount, Is.Zero);
    }

    [Test]
    public void CancelOfAQueuedRequest_PreventsPresentation()
    {
        using var carrier = new ScriptedReliableCarrier();
        var factory = new ManualTimerFactory();
        var handler = new RecordingReliableServerHandler();
        using var entered = new ManualResetEventSlim(false);
        using var release = new ManualResetEventSlim(false);
        handler.Entered = entered;
        handler.Release = release;
        TestReliableServerTransport server = StartServer(carrier, factory, handler);

        server.ReceiveFromClient(carrier.ClientEndPoint, RequestFrame(1, 5));
        Assert.That(entered.Wait(Timeout), Is.True);

        server.ReceiveFromClient(carrier.ClientEndPoint, RequestFrame(2, 6));
        server.ReceiveFromClient(carrier.ClientEndPoint, CancelFrame(2));

        release.Set();
        server.StopDispatchForTests();
        Assert.That(handler.RequestCount, Is.EqualTo(1));
    }

    [Test]
    public void ResponseTtlWhileQueued_SendsFailedAndNeverPresents()
    {
        using var carrier = new ScriptedReliableCarrier();
        var factory = new ManualTimerFactory();
        var handler = new RecordingReliableServerHandler();
        using var entered = new ManualResetEventSlim(false);
        using var release = new ManualResetEventSlim(false);
        handler.Entered = entered;
        handler.Release = release;
        TestReliableServerTransport server = StartServer(carrier, factory, handler);

        server.ReceiveFromClient(carrier.ClientEndPoint, RequestFrame(1, 5));
        Assert.That(entered.Wait(Timeout), Is.True);

        UnionDataList queued = RequestFrame(2, 6);
        server.ReceiveFromClient(carrier.ClientEndPoint, queued);

        Assert.That(factory.Handles.Count, Is.EqualTo(2));
        Assert.That(factory.Fire(1), Is.True);

        Assert.That(carrier.SentByServer.Count, Is.EqualTo(1));
        Assert.That(carrier.SentByServer[0].RequestId, Is.EqualTo(2));
        Assert.That(carrier.SentByServer[0].Completion,
            Is.EqualTo(RRReliableCompletionType.Failed));

        release.Set();
        server.StopDispatchForTests();

        Assert.That(handler.RequestCount, Is.EqualTo(1));
        Assert.That(queued.IsAlive, Is.False);
    }

    [Test]
    public void FullDispatchQueue_ReleasesTheOverflowingRequest()
    {
        using var carrier = new ScriptedReliableCarrier();
        var factory = new ManualTimerFactory();
        var handler = new RecordingReliableServerHandler();
        using var entered = new ManualResetEventSlim(false);
        using var release = new ManualResetEventSlim(false);
        handler.Entered = entered;
        handler.Release = release;
        TestReliableServerTransport server = StartServer(carrier, factory, handler,
            dispatchCapacity: 1);

        server.ReceiveFromClient(carrier.ClientEndPoint, RequestFrame(1, 5));
        Assert.That(entered.Wait(Timeout), Is.True);

        server.ReceiveFromClient(carrier.ClientEndPoint, RequestFrame(2, 6));
        UnionDataList overflow = RequestFrame(3, 7);
        server.ReceiveFromClient(carrier.ClientEndPoint, overflow);

        Assert.That(overflow.IsAlive, Is.False);
        Assert.That(handler.RequestCount, Is.EqualTo(1));

        release.Set();
        Assert.That(WaitForRequestCount(handler, 2), Is.True);
        Assert.That(handler.RequestValues, Is.EqualTo(new[] { 5, 6 }));
    }

    [Test]
    public void DuplicateRequest_IsSuppressedLiveAndAfterEveryTerminalCause()
    {
        using var carrier = new ScriptedReliableCarrier();
        var factory = new ManualTimerFactory();
        var handler = new RecordingReliableServerHandler();
        TestReliableServerTransport server = StartServer(carrier, factory, handler);

        server.ReceiveFromClient(carrier.ClientEndPoint, RequestFrame(5, 1));
        server.ReceiveFromClient(carrier.ClientEndPoint, RequestFrame(5, 1));
        Assert.That(WaitForRequestCount(handler, 1), Is.True);
        IRRReliableResponse finish = handler.LastHandle!;
        finish.Finish();
        server.ReceiveFromClient(carrier.ClientEndPoint, RequestFrame(5, 1));

        server.ReceiveFromClient(carrier.ClientEndPoint, RequestFrame(6, 2));
        Assert.That(WaitForRequestCount(handler, 2), Is.True);
        handler.LastHandle!.Reject();
        server.ReceiveFromClient(carrier.ClientEndPoint, RequestFrame(6, 2));

        server.ReceiveFromClient(carrier.ClientEndPoint, RequestFrame(7, 3));
        Assert.That(WaitForRequestCount(handler, 3), Is.True);
        server.ReceiveFromClient(carrier.ClientEndPoint, CancelFrame(7));
        server.ReceiveFromClient(carrier.ClientEndPoint, RequestFrame(7, 3));

        server.ReceiveFromClient(carrier.ClientEndPoint, RequestFrame(8, 4));
        Assert.That(WaitForRequestCount(handler, 4), Is.True);
        factory.FireAll();
        server.ReceiveFromClient(carrier.ClientEndPoint, RequestFrame(8, 4));

        Assert.That(handler.RequestValues, Is.EqualTo(new[] { 1, 2, 3, 4 }));
    }

    [Test]
    public void TwoClientsWithTheSameClientLocalId_DoNotSuppressEachOther()
    {
        using var carrier = new ScriptedReliableCarrier();
        var factory = new ManualTimerFactory();
        var handler = new RecordingReliableServerHandler();
        TestReliableServerTransport server = StartServer(carrier, factory, handler);
        IEndPoint a = new GuidEndPoint(Guid.NewGuid());
        IEndPoint b = new GuidEndPoint(Guid.NewGuid());

        server.ReceiveFromClient(a, RequestFrame(5, 1));
        server.ReceiveFromClient(b, RequestFrame(5, 2));

        Assert.That(WaitForRequestCount(handler, 2), Is.True);
        Assert.That(handler.RequestValues, Is.EqualTo(new[] { 1, 2 }));
    }

    [Test]
    public void CompletedWindow_IsPerSessionAndFullAfterEveryId()
    {
        var session = new RRReliableServerSession();

        for (int id = 0; id <= ushort.MaxValue; id++)
        {
            session.MarkCompleted((ushort)id, null);
        }

        Assert.That(session.IsFull, Is.True);
        Assert.That(session.IsCompleted(0), Is.True);
        Assert.That(session.IsCompleted(ushort.MaxValue), Is.True);
        Assert.That(new RRReliableServerSession().IsFull, Is.False);
    }

    [Test]
    public void OnRequested_IsSerializedAcrossRequests()
    {
        using var carrier = new ScriptedReliableCarrier();
        var factory = new ManualTimerFactory();
        var handler = new RecordingReliableServerHandler();
        using var entered = new ManualResetEventSlim(false);
        using var release = new ManualResetEventSlim(false);
        handler.Entered = entered;
        handler.Release = release;
        TestReliableServerTransport server = StartServer(carrier, factory, handler);

        server.ReceiveFromClient(carrier.ClientEndPoint, RequestFrame(1, 1));
        Assert.That(entered.Wait(Timeout), Is.True);

        server.ReceiveFromClient(carrier.ClientEndPoint, RequestFrame(2, 2));
        Assert.That(handler.RequestCount, Is.EqualTo(1));

        release.Set();
        Assert.That(WaitForRequestCount(handler, 2), Is.True);
        Assert.That(handler.MaxActiveCount, Is.EqualTo(1));
    }

    [Test]
    public void ThrowingHandler_RejectsTheUndecidedHandle()
    {
        using var carrier = new ScriptedReliableCarrier();
        var factory = new ManualTimerFactory();
        var handler = new RecordingReliableServerHandler(ServerHandlerMode.Throw);
        TestReliableServerTransport server = StartServer(carrier, factory, handler);

        server.ReceiveFromClient(carrier.ClientEndPoint, RequestFrame(1, 5));

        Assert.That(WaitForRequestCount(handler, 1), Is.True);
        Assert.That(SpinWait.SpinUntil(() => carrier.SentByServer.Count == 1, Timeout), Is.True);
        Assert.That(carrier.SentByServer[0].Completion,
            Is.EqualTo(RRReliableCompletionType.Rejected));
        Assert.That(server.IsValid, Is.True);
    }

    [Test]
    public void RequestBeforeOnStarted_IsHeldUntilTheReadyGateOpens()
    {
        using var carrier = new ScriptedReliableCarrier();
        var factory = new ManualTimerFactory();
        var handler = new RecordingReliableServerHandler();
        var server = new TestReliableServerTransport(carrier, TimeSpan.FromSeconds(30), factory);
        _servers.Add(server);
        Assert.That(server.Init(handler), Is.True);
        Assert.That(server.StartDispatchForTests(), Is.True);

        server.ReceiveFromClient(carrier.ClientEndPoint, RequestFrame(1, 5));
        Assert.That(handler.RequestCount, Is.Zero);

        Assert.That(server.Start(_ => { }), Is.True);
        Assert.That(WaitForRequestCount(handler, 1), Is.True);
    }

    [Test]
    public void FullPendingListBeforeStart_ReleasesTheOverflowingFrame()
    {
        using var carrier = new ScriptedReliableCarrier();
        var factory = new ManualTimerFactory();
        var handler = new RecordingReliableServerHandler();
        var server = new TestReliableServerTransport(carrier, TimeSpan.FromSeconds(30), factory,
            dispatchCapacity: 2);
        _servers.Add(server);
        Assert.That(server.Init(handler), Is.True);
        Assert.That(server.StartDispatchForTests(), Is.True);

        server.ReceiveFromClient(carrier.ClientEndPoint, RequestFrame(1, 1));
        server.ReceiveFromClient(carrier.ClientEndPoint, RequestFrame(2, 2));
        UnionDataList overflow = RequestFrame(3, 3);
        server.ReceiveFromClient(carrier.ClientEndPoint, overflow);

        Assert.That(overflow.IsAlive, Is.False);

        Assert.That(server.Start(_ => { }), Is.True);
        Assert.That(WaitForRequestCount(handler, 2), Is.True);
        Assert.That(handler.RequestValues, Is.EqualTo(new[] { 1, 2 }));
    }

    [Test]
    public void StopDispatch_ReleasesARequestStillQueued()
    {
        using var carrier = new ScriptedReliableCarrier();
        var factory = new ManualTimerFactory();
        var handler = new RecordingReliableServerHandler();
        var server = new TestReliableServerTransport(carrier, TimeSpan.FromSeconds(30), factory);
        _servers.Add(server);
        Assert.That(server.Init(handler), Is.True);
        Assert.That(server.StartDispatchForTests(), Is.True);

        UnionDataList queued = RequestFrame(1, 5);
        server.ReceiveFromClient(carrier.ClientEndPoint, queued);
        server.StopDispatchForTests();

        Assert.That(queued.IsAlive, Is.False);
        Assert.That(handler.RequestCount, Is.Zero);
    }

    [Test]
    public void OnSessionClosed_ClosesOnlyThatSession()
    {
        using var carrier = new ScriptedReliableCarrier();
        var factory = new ManualTimerFactory();
        var handler = new RecordingReliableServerHandler();
        TestReliableServerTransport server = StartServer(carrier, factory, handler);
        IEndPoint a = new GuidEndPoint(Guid.NewGuid());
        IEndPoint b = new GuidEndPoint(Guid.NewGuid());

        server.ReceiveFromClient(a, RequestFrame(1, 1));
        server.ReceiveFromClient(b, RequestFrame(1, 2));
        Assert.That(WaitForRequestCount(handler, 2), Is.True);
        IRRReliableResponse handleA = handler.Handles[0];
        IRRReliableResponse handleB = handler.Handles[1];

        server.CloseSession(a);

        Assert.That(handleA.Response(Payload(10)), Is.EqualTo(SendResult.Error));
        Assert.That(handleB.Response(Payload(20)), Is.EqualTo(SendResult.Ok));

        server.ReceiveFromClient(a, RequestFrame(2, 3));
        Assert.That(WaitForRequestCount(handler, 3), Is.True);
    }

    [Test]
    public void BlockedResponse_WithConcurrentStop_DoesNotHang()
    {
        using var carrier = new ScriptedReliableCarrier();
        var factory = new ManualTimerFactory();
        var handler = new RecordingReliableServerHandler();
        TestReliableServerTransport server = StartServer(carrier, factory, handler);
        server.ReceiveFromClient(carrier.ClientEndPoint, RequestFrame(1, 5));
        Assert.That(WaitForRequestCount(handler, 1), Is.True);
        IRRReliableResponse handle = handler.LastHandle!;

        using var entered = new ManualResetEventSlim(false);
        using var release = new ManualResetEventSlim(false);
        server.SendOverride = (_, frame) =>
        {
            entered.Set();
            release.Wait(Timeout);
            frame.Release();
            return SendResult.Ok;
        };

        Task<SendResult> response = Task.Run(() => handle.Response(Payload(10)));
        Assert.That(entered.Wait(Timeout), Is.True);

        Task stop = Task.Run(() => server.Stop());
        Assert.That(stop.Wait(Timeout), Is.True, "Stop must not wait for an in-flight response");

        release.Set();
        Assert.That(response.Wait(Timeout), Is.True);
    }

    [Test]
    public void TwoThreadsOnOneHandle_ProduceOneTerminalResult()
    {
        using var carrier = new ScriptedReliableCarrier();
        var factory = new ManualTimerFactory();
        var handler = new RecordingReliableServerHandler();
        TestReliableServerTransport server = StartServer(carrier, factory, handler);
        server.ReceiveFromClient(carrier.ClientEndPoint, RequestFrame(1, 5));
        Assert.That(WaitForRequestCount(handler, 1), Is.True);
        IRRReliableResponse handle = handler.LastHandle!;

        Task<SendResult> first = Task.Run(() => handle.Finish());
        Task<SendResult> second = Task.Run(() => handle.Finish());
        Assert.That(Task.WaitAll(new[] { first, second }, Timeout), Is.True);

        int okCount = (first.Result == SendResult.Ok ? 1 : 0) +
                      (second.Result == SendResult.Ok ? 1 : 0);
        Assert.That(okCount, Is.EqualTo(1));
        Assert.That(carrier.SentByServer.Count, Is.EqualTo(1));
    }

    private TestReliableServerTransport StartServer(ScriptedReliableCarrier carrier,
        ManualTimerFactory factory, RecordingReliableServerHandler handler,
        int dispatchCapacity = 1024, TimeSpan? responseTtl = null)
    {
        var server = new TestReliableServerTransport(carrier,
            responseTtl ?? TimeSpan.FromSeconds(30), factory, dispatchCapacity);
        _servers.Add(server);
        Assert.That(server.Init(handler), Is.True);
        Assert.That(server.Start(_ => { }), Is.True);
        return server;
    }

    private static bool WaitForRequestCount(RecordingReliableServerHandler handler, int count)
    {
        return SpinWait.SpinUntil(() => handler.RequestCount >= count, Timeout);
    }

    private static UnionDataList Payload(params int[] values)
    {
        UnionDataList payload = MemoryRental.Shared.CollectablePool.Acquire<UnionDataList>();

        foreach (int value in values)
        {
            payload.PutLast(new UnionData(value));
        }

        return payload;
    }

    private static UnionDataList RequestFrame(ushort id, int value)
    {
        UnionDataList frame = Payload(value);
        RRReliableFrame.EncodeRequest(frame, id);
        return frame;
    }

    private static UnionDataList CancelFrame(ushort id)
    {
        UnionDataList frame = MemoryRental.Shared.CollectablePool.Acquire<UnionDataList>();
        RRReliableFrame.EncodeCancel(frame, id);
        return frame;
    }
}
