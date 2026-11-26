using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Actuarius.Memory;
using Pontifex.Endpoints;
using Pontifex.RR;
using Pontifex.RR.Reliable;
using Pontifex.StopReasons;
using Pontifex.Utils;

namespace Pontifex.Tests.Transports;

[TestFixture]
[Category("Transports")]
[Category("Transports.RR")]
[Category("Transports.RR.Reliable")]
[Category("Transports.RR.Reliable.Client")]
public sealed class RRReliableClientTransportTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private readonly List<RRReliableClientTransport> _clients = new List<RRReliableClientTransport>();

    [TearDown]
    public void TearDown()
    {
        foreach (RRReliableClientTransport client in _clients)
        {
            client.Stop();
        }

        _clients.Clear();
    }

    [Test]
    public void OrderedResponses_AreDeliveredBeforeFinishNormal()
    {
        using var carrier = new ScriptedReliableCarrier();
        var factory = new ManualTimerFactory();
        TestReliableClientTransport client = StartClient(carrier, factory);
        var handler = new RecordingReliableClientHandler();
        Assert.That(client.Request(Payload(1), handler), Is.EqualTo(SendResult.Ok));
        ushort id = handler.RequestId!.Value;

        client.Receive(ResponseFrame(id, 1, 10));
        client.Receive(ResponseFrame(id, 2, 20));
        client.Receive(FinishFrame(id, RRReliableCompletionType.Normal));

        Assert.That(handler.Finished.Wait(Timeout), Is.True);
        Assert.That(handler.ResponseValues, Is.EqualTo(new[] { 10, 20 }));
        Assert.That(handler.Finishes, Is.EqualTo(new[] { RRReliableCompletionType.Normal }));
    }

    [Test]
    public void ZeroResponseFinish_IsDeliveredImmediately()
    {
        using var carrier = new ScriptedReliableCarrier();
        var factory = new ManualTimerFactory();
        TestReliableClientTransport client = StartClient(carrier, factory);
        var handler = new RecordingReliableClientHandler();
        Assert.That(client.Request(Payload(1), handler), Is.EqualTo(SendResult.Ok));

        client.Receive(FinishFrame(handler.RequestId!.Value, RRReliableCompletionType.Normal));

        Assert.That(handler.Finished.Wait(Timeout), Is.True);
        Assert.That(handler.ResponseCount, Is.Zero);
        Assert.That(handler.Finishes, Is.EqualTo(new[] { RRReliableCompletionType.Normal }));
    }

    [Test]
    public void DuplicateResponseOrdinal_ProducesOneCallback()
    {
        using var carrier = new ScriptedReliableCarrier();
        var factory = new ManualTimerFactory();
        TestReliableClientTransport client = StartClient(carrier, factory);
        var handler = new RecordingReliableClientHandler();
        Assert.That(client.Request(Payload(1), handler), Is.EqualTo(SendResult.Ok));
        ushort id = handler.RequestId!.Value;

        client.Receive(ResponseFrame(id, 1, 10));
        client.Receive(ResponseFrame(id, 1, 11));
        client.Receive(FinishFrame(id, RRReliableCompletionType.Normal));

        Assert.That(handler.Finished.Wait(Timeout), Is.True);
        Assert.That(handler.ResponseValues, Is.EqualTo(new[] { 10 }));
    }

    [Test]
    public void ResponseAfterFinish_IsDropped()
    {
        using var carrier = new ScriptedReliableCarrier();
        var factory = new ManualTimerFactory();
        TestReliableClientTransport client = StartClient(carrier, factory);
        var handler = new RecordingReliableClientHandler();
        Assert.That(client.Request(Payload(1), handler), Is.EqualTo(SendResult.Ok));
        ushort id = handler.RequestId!.Value;

        client.Receive(FinishFrame(id, RRReliableCompletionType.Normal));
        Assert.That(handler.Finished.Wait(Timeout), Is.True);

        client.Receive(ResponseFrame(id, 1, 10));

        Assert.That(handler.ResponseCount, Is.Zero);
    }

    [Test]
    public void OutOfOrderResponse_IsHeldUntilTheGapFillsAndFinishWaits()
    {
        using var carrier = new ScriptedReliableCarrier();
        var factory = new ManualTimerFactory();
        TestReliableClientTransport client = StartClient(carrier, factory);
        var handler = new RecordingReliableClientHandler();
        Assert.That(client.Request(Payload(1), handler), Is.EqualTo(SendResult.Ok));
        ushort id = handler.RequestId!.Value;

        client.Receive(ResponseFrame(id, 2, 20));
        client.Receive(FinishFrame(id, RRReliableCompletionType.Normal));
        Assert.That(handler.ResponseCount, Is.Zero);
        Assert.That(handler.FinishCount, Is.Zero);

        client.Receive(ResponseFrame(id, 1, 10));

        Assert.That(handler.Finished.Wait(Timeout), Is.True);
        Assert.That(handler.ResponseValues, Is.EqualTo(new[] { 10, 20 }));
    }

    [Test]
    public void LowerAndDuplicateOrdinals_AreDropped()
    {
        using var carrier = new ScriptedReliableCarrier();
        var factory = new ManualTimerFactory();
        TestReliableClientTransport client = StartClient(carrier, factory);
        var handler = new RecordingReliableClientHandler();
        Assert.That(client.Request(Payload(1), handler), Is.EqualTo(SendResult.Ok));
        ushort id = handler.RequestId!.Value;

        client.Receive(ResponseFrame(id, 1, 10));
        client.Receive(ResponseFrame(id, 1, 11));
        client.Receive(ResponseFrame(id, 2, 20));
        client.Receive(FinishFrame(id, RRReliableCompletionType.Normal));

        Assert.That(handler.Finished.Wait(Timeout), Is.True);
        Assert.That(handler.ResponseValues, Is.EqualTo(new[] { 10, 20 }));
    }

    [Test]
    public void NonOkRequest_SchedulesNoFinish()
    {
        using var carrier = new ScriptedReliableCarrier();
        var factory = new ManualTimerFactory();
        TestReliableClientTransport client = StartClient(carrier, factory);
        var handler = new RecordingReliableClientHandler();
        client.SendOverride = frame =>
        {
            frame.Release();
            return SendResult.Error;
        };

        Assert.That(client.Request(Payload(1), handler), Is.EqualTo(SendResult.Error));

        Assert.That(client.ScheduledTerminalCount, Is.Zero);
        Assert.That(handler.IsBound, Is.False);
        Assert.That(handler.RequestId, Is.EqualTo((ushort)1));
    }

    [Test]
    public void ProvisionalBuffer_IsReleasedWhenTheRequestReturnsNonOk()
    {
        using var carrier = new ScriptedReliableCarrier();
        var factory = new ManualTimerFactory();
        TestReliableClientTransport client = StartClient(carrier, factory);
        var handler = new RecordingReliableClientHandler();
        UnionDataList buffered = null!;
        client.SendOverride = frame =>
        {
            frame.Release();
            ushort id = handler.RequestId!.Value;
            buffered = ResponseFrame(id, 1, 10);
            client.Receive(buffered);
            return SendResult.Error;
        };

        Assert.That(client.Request(Payload(1), handler), Is.EqualTo(SendResult.Error));

        Assert.That(buffered.IsAlive, Is.False);
        Assert.That(handler.ResponseCount, Is.Zero);
        Assert.That(handler.FinishCount, Is.Zero);
    }

    [Test]
    public void FullIdTable_ReturnsBufferOverflow()
    {
        using var carrier = new ScriptedReliableCarrier();
        var factory = new ManualTimerFactory();
        TestReliableClientTransport client = StartClient(carrier, factory);
        var filler = new RecordingReliableClientHandler();

        for (int id = 0; id <= ushort.MaxValue; id++)
        {
            ushort value = (ushort)id;
            Assert.That(client.RequestsForTests.TryAdd(value, filler,
                new RRReliableRequestState(value, filler)), Is.True);
        }

        var handler = new RecordingReliableClientHandler();
        UnionDataList payload = Payload(1);

        Assert.That(client.Request(payload, handler), Is.EqualTo(SendResult.BufferOverflow));
        Assert.That(payload.IsAlive, Is.False);
        Assert.That(handler.IsBound, Is.False);

        client.RequestsForTests.TakeAll();
    }

    [Test]
    public void StopDuringSend_ReturnsErrorWithNoCallback()
    {
        using var carrier = new ScriptedReliableCarrier();
        var factory = new ManualTimerFactory();
        TestReliableClientTransport client = StartClient(carrier, factory);
        var handler = new RecordingReliableClientHandler();
        client.SendOverride = frame =>
        {
            frame.Release();
            client.Stop();
            return SendResult.Error;
        };

        Assert.That(client.Request(Payload(1), handler), Is.EqualTo(SendResult.Error));

        Assert.That(handler.FinishCount, Is.Zero);
        Assert.That(handler.ResponseCount, Is.Zero);
        Assert.That(client.ScheduledTerminalCount, Is.Zero);
    }

    [Test]
    public void SynchronousFinishInsideSendFrame_IsBufferedUntilOk()
    {
        using var carrier = new ScriptedReliableCarrier();
        var factory = new ManualTimerFactory();
        TestReliableClientTransport client = StartClient(carrier, factory);
        var handler = new RecordingReliableClientHandler();
        int finishCountInsideSend = -1;
        client.SendOverride = frame =>
        {
            frame.Release();
            ushort id = handler.RequestId!.Value;
            client.Receive(FinishFrame(id, RRReliableCompletionType.Normal));
            finishCountInsideSend = handler.FinishCount;
            return SendResult.Ok;
        };

        Assert.That(client.Request(Payload(1), handler), Is.EqualTo(SendResult.Ok));

        Assert.That(finishCountInsideSend, Is.Zero);
        Assert.That(handler.Finished.Wait(Timeout), Is.True);
        Assert.That(handler.Finishes, Is.EqualTo(new[] { RRReliableCompletionType.Normal }));
    }

    [Test]
    public void StopInjectedAtTheBeforeSendGate_ReturnsNotConnectedWithNoCallback()
    {
        using var carrier = new ScriptedReliableCarrier();
        var factory = new ManualTimerFactory();
        TestReliableClientTransport client = StartClient(carrier, factory);
        var handler = new RecordingReliableClientHandler();
        client.ExposeBeforeSendGate = () => client.Stop();

        Assert.That(client.Request(Payload(1), handler), Is.EqualTo(SendResult.NotConnected));
        Assert.That(handler.IsBound, Is.False);
        Assert.That(handler.FinishCount, Is.Zero);
        Assert.That(client.SendCalls, Is.Zero);
    }

    [Test]
    public void CancelInjectedAtTheBeforeSendGate_ReturnsErrorWithNoCallback()
    {
        using var carrier = new ScriptedReliableCarrier();
        var factory = new ManualTimerFactory();
        TestReliableClientTransport client = StartClient(carrier, factory);
        var handler = new RecordingReliableClientHandler();
        client.ExposeBeforeSendGate = () => handler.Cancel();

        Assert.That(client.Request(Payload(1), handler), Is.EqualTo(SendResult.Error));
        Assert.That(handler.IsBound, Is.False);
        Assert.That(handler.FinishCount, Is.Zero);
        Assert.That(client.SendCalls, Is.Zero);
    }

    [Test]
    [Category("Transports.Concurrency")]
    public void TerminalKeepsTheHandlerBoundUntilTheFinishCallbackReturns()
    {
        using var carrier = new ScriptedReliableCarrier();
        var factory = new ManualTimerFactory();
        TestReliableClientTransport client = StartClient(carrier, factory);
        var handler = new RecordingReliableClientHandler();
        using var entered = new ManualResetEventSlim(false);
        using var release = new ManualResetEventSlim(false);
        handler.OnFinish = _ =>
        {
            entered.Set();
            release.Wait(Timeout);
        };

        Assert.That(client.Request(Payload(1), handler), Is.EqualTo(SendResult.Ok));
        client.Receive(FinishFrame(handler.RequestId!.Value, RRReliableCompletionType.Normal));

        Assert.That(entered.Wait(Timeout), Is.True);
        Assert.That(handler.IsBound, Is.True);
        Assert.Throws<InvalidOperationException>(() => client.Request(Payload(2), handler));

        release.Set();
        Assert.That(handler.Finished.Wait(Timeout), Is.True);
        Assert.That(SpinWait.SpinUntil(() => !handler.IsBound, Timeout), Is.True);

        Assert.That(client.Request(Payload(2), handler), Is.EqualTo(SendResult.Ok));
    }

    [Test]
    public void RejectTerminal_ReachesTheClientAsRejected()
    {
        using var carrier = new ScriptedReliableCarrier();
        var factory = new ManualTimerFactory();
        TestReliableClientTransport client = StartClient(carrier, factory);
        var handler = new RecordingReliableClientHandler();
        Assert.That(client.Request(Payload(1), handler), Is.EqualTo(SendResult.Ok));

        client.Receive(FinishFrame(handler.RequestId!.Value, RRReliableCompletionType.Rejected));

        Assert.That(handler.Finished.Wait(Timeout), Is.True);
        Assert.That(handler.Finishes, Is.EqualTo(new[] { RRReliableCompletionType.Rejected }));
    }

    [Test]
    public void LostTerminal_FailsAtTheRequestTtlWithOneFinish()
    {
        using var carrier = new ScriptedReliableCarrier();
        var factory = new ManualTimerFactory();
        TestReliableClientTransport client = StartClient(carrier, factory);
        var handler = new RecordingReliableClientHandler();
        Assert.That(client.Request(Payload(1), handler), Is.EqualTo(SendResult.Ok));

        Assert.That(factory.PendingCount, Is.EqualTo(1));
        Assert.That(factory.FireAll(), Is.EqualTo(1));

        Assert.That(handler.Finished.Wait(Timeout), Is.True);
        Assert.That(handler.Finishes, Is.EqualTo(new[] { RRReliableCompletionType.Failed }));
        Assert.That(handler.FinishCount, Is.EqualTo(1));
        Assert.That(factory.PendingCount, Is.Zero);
    }

    [Test]
    [Category("Transports.Concurrency")]
    public void CancelOutsideACallback_DeliversOneCanceledAndSuppressesLateResponses()
    {
        using var carrier = new ScriptedReliableCarrier();
        var factory = new ManualTimerFactory();
        TestReliableClientTransport client = StartClient(carrier, factory);
        var handler = new RecordingReliableClientHandler();
        Assert.That(client.Request(Payload(1), handler), Is.EqualTo(SendResult.Ok));
        ushort id = handler.RequestId!.Value;

        handler.Cancel();

        Assert.That(handler.Finished.Wait(Timeout), Is.True);
        Assert.That(handler.Finishes, Is.EqualTo(new[] { RRReliableCompletionType.Canceled }));
        Assert.That(SpinWait.SpinUntil(() => factory.PendingCount == 0, Timeout), Is.True);
        Assert.That(carrier.SentByClient.Exists(frame => frame.FrameType == RRReliableFrameType.Cancel),
            Is.True);

        client.Receive(ResponseFrame(id, 1, 10));
        Assert.That(handler.ResponseCount, Is.Zero);
    }

    [Test]
    public void CancelInsideAResponseCallback_DefersTheTerminal()
    {
        using var carrier = new ScriptedReliableCarrier();
        var factory = new ManualTimerFactory();
        TestReliableClientTransport client = StartClient(carrier, factory);
        var handler = new RecordingReliableClientHandler();
        handler.OnResponse = _ => handler.Cancel();
        Assert.That(client.Request(Payload(1), handler), Is.EqualTo(SendResult.Ok));

        client.Receive(ResponseFrame(handler.RequestId!.Value, 1, 10));

        Assert.That(handler.Finished.Wait(Timeout), Is.True);
        Assert.That(handler.ResponseCount, Is.EqualTo(1));
        Assert.That(handler.Finishes, Is.EqualTo(new[] { RRReliableCompletionType.Canceled }));
    }

    [Test]
    [Category("Transports.Concurrency")]
    public void Stop_SchedulesOneFailedPerPendingRequestBeforeOnStoppedAndDeliversAfter()
    {
        using var carrier = new ScriptedReliableCarrier();
        var factory = new ManualTimerFactory();
        var client = new TestReliableClientTransport(carrier, TimeSpan.FromSeconds(30), factory);
        _clients.Add(client);
        int scheduledInOnStopped = -1;
        Assert.That(client.Start(_ => scheduledInOnStopped = client.ScheduledTerminalCount), Is.True);
        var first = new RecordingReliableClientHandler();
        var second = new RecordingReliableClientHandler();
        Assert.That(client.Request(Payload(1), first), Is.EqualTo(SendResult.Ok));
        Assert.That(client.Request(Payload(2), second), Is.EqualTo(SendResult.Ok));

        using var gate = new ManualResetEventSlim(false);
        client.PumpGateForTests = () => gate.Wait(Timeout);

        Assert.That(client.Stop(), Is.True);
        Assert.That(scheduledInOnStopped, Is.EqualTo(2));

        gate.Set();
        Assert.That(first.Finished.Wait(Timeout), Is.True);
        Assert.That(second.Finished.Wait(Timeout), Is.True);
        Assert.That(first.Finishes, Is.EqualTo(new[] { RRReliableCompletionType.Failed }));
        Assert.That(second.Finishes, Is.EqualTo(new[] { RRReliableCompletionType.Failed }));
        Assert.That(SpinWait.SpinUntil(() => client.ScheduledTerminalCount == 0, Timeout), Is.True);
    }

    [Test]
    public void StopFromInsideAResponseCallback_DoesNotDeadlockAndDeliversFailed()
    {
        using var carrier = new ScriptedReliableCarrier();
        var factory = new ManualTimerFactory();
        var client = new TestReliableClientTransport(carrier, TimeSpan.FromSeconds(30), factory);
        _clients.Add(client);
        Assert.That(client.Start(_ => { }), Is.True);
        var handler = new RecordingReliableClientHandler();
        handler.OnResponse = _ => client.Stop(new TextFail("test", "stop from callback"));
        Assert.That(client.Request(Payload(1), handler), Is.EqualTo(SendResult.Ok));

        client.Receive(ResponseFrame(handler.RequestId!.Value, 1, 10));

        Assert.That(handler.Finished.Wait(Timeout), Is.True);
        Assert.That(handler.Finishes, Is.EqualTo(new[] { RRReliableCompletionType.Failed }));
        Assert.That(client.IsStarted, Is.False);
        Assert.That(client.IsValid, Is.True);
    }

    [Test]
    public void RequestId_IsPublishedBeforeSendFrame()
    {
        using var carrier = new ScriptedReliableCarrier();
        var factory = new ManualTimerFactory();
        TestReliableClientTransport client = StartClient(carrier, factory, randomSeed: 7);
        var handler = new RecordingReliableClientHandler();
        ushort observed = 0;
        client.SendOverride = frame =>
        {
            observed = handler.RequestId!.Value;
            frame.Release();
            return SendResult.Error;
        };

        Assert.That(client.Request(Payload(1), handler), Is.EqualTo(SendResult.Error));
        Assert.That(observed, Is.EqualTo(7));
    }

    [Test]
    public void RequestIds_StartAtTheRandomSeedAndWrap()
    {
        using var carrier = new ScriptedReliableCarrier();
        var factory = new ManualTimerFactory();
        TestReliableClientTransport client = StartClient(carrier, factory,
            randomSeed: ushort.MaxValue);
        var first = new RecordingReliableClientHandler();
        var second = new RecordingReliableClientHandler();

        Assert.That(client.Request(Payload(1), first), Is.EqualTo(SendResult.Ok));
        Assert.That(client.Request(Payload(2), second), Is.EqualTo(SendResult.Ok));

        Assert.That(first.RequestId, Is.EqualTo(ushort.MaxValue));
        Assert.That(second.RequestId, Is.Zero);
    }

    [Test]
    public void ResponseFromAForeignRoute_IsReleasedWithoutACallback()
    {
        using var carrier = new ScriptedReliableCarrier();
        var factory = new ManualTimerFactory();
        TestReliableClientTransport client = StartClient(carrier, factory);
        var handler = new RecordingReliableClientHandler();
        Assert.That(client.Request(Payload(1), handler), Is.EqualTo(SendResult.Ok));

        UnionDataList frame = ResponseFrame(handler.RequestId!.Value, 1, 10);
        client.ReceiveFromForeignRoute(new GuidEndPoint(Guid.NewGuid()), frame);

        Assert.That(frame.IsAlive, Is.False);
        Assert.That(handler.ResponseCount, Is.Zero);
    }

    [Test]
    public void Request_WithHandlerChecks_ThrowsBeforeConsumingThePayload()
    {
        using var carrier = new ScriptedReliableCarrier();
        var factory = new ManualTimerFactory();
        TestReliableClientTransport client = StartClient(carrier, factory);
        UnionDataList payload = Payload(1);

        Assert.Throws<ArgumentNullException>(() => client.Request(payload, null!));
        Assert.That(payload.IsAlive, Is.True);

        Assert.Throws<ArgumentException>(() =>
            client.Request(payload, new PlainReliableClientHandler()));
        Assert.That(payload.IsAlive, Is.True);

        var handler = new RecordingReliableClientHandler();
        Assert.That(client.Request(payload, handler), Is.EqualTo(SendResult.Ok));

        UnionDataList second = Payload(2);

        try
        {
            Assert.Throws<InvalidOperationException>(() => client.Request(second, handler));
            Assert.That(second.IsAlive, Is.True);
        }
        finally
        {
            second.Release();
        }
    }

    [Test]
    public void Request_AfterStop_ReturnsNotConnectedAndReleases()
    {
        using var carrier = new ScriptedReliableCarrier();
        var factory = new ManualTimerFactory();
        TestReliableClientTransport client = StartClient(carrier, factory);
        client.Stop();
        var handler = new RecordingReliableClientHandler();
        UnionDataList payload = Payload(1);

        Assert.That(client.Request(payload, handler), Is.EqualTo(SendResult.NotConnected));
        Assert.That(payload.IsAlive, Is.False);
        Assert.That(handler.IsBound, Is.False);
        Assert.That(handler.FinishCount, Is.Zero);
    }

    [Test]
    public void Request_AboveTheMessageLimit_ReturnsMessageTooBigAndReleases()
    {
        using var carrier = new ScriptedReliableCarrier();
        var factory = new ManualTimerFactory();
        TestReliableClientTransport client = StartClient(carrier, factory, messageMaxByteSize: 10);
        var handler = new RecordingReliableClientHandler();
        UnionDataList payload = Payload(1, 2, 3);

        Assert.That(client.Request(payload, handler), Is.EqualTo(SendResult.MessageTooBig));
        Assert.That(payload.IsAlive, Is.False);
        Assert.That(handler.IsBound, Is.False);
    }

    [Test]
    public void Request_WithNullPayload_ReturnsInvalidMessage()
    {
        using var carrier = new ScriptedReliableCarrier();
        var factory = new ManualTimerFactory();
        TestReliableClientTransport client = StartClient(carrier, factory);
        var handler = new RecordingReliableClientHandler();

        Assert.That(client.Request(null!, handler), Is.EqualTo(SendResult.InvalidMessage));
        Assert.That(handler.IsBound, Is.False);
    }

    [Test]
    public void ThrowingClientHandler_IsIsolatedAndTheTerminalStillArrives()
    {
        using var carrier = new ScriptedReliableCarrier();
        var factory = new ManualTimerFactory();
        TestReliableClientTransport client = StartClient(carrier, factory);
        var handler = new RecordingReliableClientHandler { ThrowOnResponse = true };
        Assert.That(client.Request(Payload(1), handler), Is.EqualTo(SendResult.Ok));
        ushort id = handler.RequestId!.Value;

        client.Receive(ResponseFrame(id, 1, 10));
        client.Receive(FinishFrame(id, RRReliableCompletionType.Normal));

        Assert.That(handler.Finished.Wait(Timeout), Is.True);
        Assert.That(handler.ResponseCount, Is.EqualTo(1));
        Assert.That(handler.Finishes, Is.EqualTo(new[] { RRReliableCompletionType.Normal }));
        Assert.That(client.IsValid, Is.True);
    }

    [Test]
    [Category("Transports.Concurrency")]
    public void ConcurrentResponsesForDifferentRequests_AreDeliveredSafely()
    {
        using var carrier = new ScriptedReliableCarrier();
        var factory = new ManualTimerFactory();
        TestReliableClientTransport client = StartClient(carrier, factory);
        var first = new RecordingReliableClientHandler();
        var second = new RecordingReliableClientHandler();
        Assert.That(client.Request(Payload(1), first), Is.EqualTo(SendResult.Ok));
        Assert.That(client.Request(Payload(2), second), Is.EqualTo(SendResult.Ok));

        Task taskA = Task.Run(() =>
            client.Receive(ResponseFrame(first.RequestId!.Value, 1, 10)));
        Task taskB = Task.Run(() =>
            client.Receive(ResponseFrame(second.RequestId!.Value, 1, 20)));

        Assert.That(Task.WaitAll(new[] { taskA, taskB }, Timeout), Is.True);
        Assert.That(first.Responded.Wait(Timeout), Is.True);
        Assert.That(second.Responded.Wait(Timeout), Is.True);
    }

    [Test]
    public void Request_WhenTheTimerFactoryThrows_ReturnsErrorAndUnbindsTheHandler()
    {
        using var carrier = new ScriptedReliableCarrier();
        var client = new TestReliableClientTransport(carrier, TimeSpan.FromSeconds(30),
            new ThrowingTimerFactory());
        _clients.Add(client);
        Assert.That(client.Start(_ => { }), Is.True);
        var handler = new RecordingReliableClientHandler();

        Assert.That(client.Request(Payload(1), handler), Is.EqualTo(SendResult.Error));
        Assert.Multiple(() =>
        {
            Assert.That(client.PendingCountForTests, Is.Zero);
            Assert.That(handler.IsBound, Is.False);
            Assert.That(handler.ResponseCount, Is.Zero);
            Assert.That(handler.FinishCount, Is.Zero);
        });
    }

    private sealed class ThrowingTimerFactory : IRRReliableTimerFactory
    {
        public IDisposable Schedule(TimeSpan delay, Action callback)
        {
            throw new InvalidOperationException("Expected timer-factory failure.");
        }
    }

    private TestReliableClientTransport StartClient(ScriptedReliableCarrier carrier,
        ManualTimerFactory factory, ushort randomSeed = 1, int messageMaxByteSize = 64,
        TimeSpan? requestTtl = null)
    {
        var client = new TestReliableClientTransport(carrier,
            requestTtl ?? TimeSpan.FromSeconds(30), factory, randomSeed, messageMaxByteSize);
        _clients.Add(client);
        Assert.That(client.Start(_ => { }), Is.True);
        return client;
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

    private static UnionDataList ResponseFrame(ushort id, uint ordinal, int value)
    {
        UnionDataList frame = Payload(value);
        RRReliableFrame.EncodeResponse(frame, id, ordinal);
        return frame;
    }

    private static UnionDataList FinishFrame(ushort id, RRReliableCompletionType completion)
    {
        UnionDataList frame = MemoryRental.Shared.CollectablePool.Acquire<UnionDataList>();
        RRReliableFrame.EncodeFinish(frame, id, completion);
        return frame;
    }
}
