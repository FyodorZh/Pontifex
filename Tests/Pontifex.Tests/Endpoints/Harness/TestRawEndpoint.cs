using System;
using System.Collections.Generic;
using Actuarius.Memory;
using Pontifex.Endpoints;
using Pontifex.Raw;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Tests.Endpoints;

/// <summary>
/// Test double for <see cref="RawEndpoint"/>. It exposes the protected surface of the endpoint and
/// records the transport hooks so tests can assert observable effects. Completion of the abstract
/// hooks is explicit: <see cref="BeginStart"/> and <see cref="BeginStop"/> only record the call, and
/// tests drive completion through <see cref="CompleteStart"/>/<see cref="CompleteStop"/> or the
/// step-by-step queue controls.
/// </summary>
internal sealed class TestRawEndpoint : RawEndpoint, IDisposable
{
    // No initializer: the queued is created during the base constructor, before field initializers run.
    private ManualSerialActionQueue? _queue;
    private readonly List<UnionDataList> _capturedSends = new();

    public TestRawEndpoint(
        IRawHandler handler,
        IEndPoint? remoteEndPoint = null,
        string transportName = "TestTransport",
        int messageMaxByteSize = 1024,
        long bufferCapacityBytes = 1024 * 1024,
        IMemoryRental? memory = null,
        ILogger? log = null)
        : base(
            transportName,
            remoteEndPoint ?? VoidEndPoint.Instance,
            handler,
            messageMaxByteSize,
            bufferCapacityBytes,
            memory ?? MemoryRental.Shared,
            log ?? new Logger([]))
    {
    }

    protected override ISerialActionQueue SerialQueueConstructor(Action<Exception> onError)
    {
        return _queue = new ManualSerialActionQueue(onError);
    }

    #region Queue control

    public void DrainOne() => _queue!.DrainOne();

    public void DrainAll() => _queue!.DrainAll();

    #endregion

    #region Lifecycle helpers

    public void CompleteStart()
    {
        ExposedFinishStart(NoOp);
        DrainAll();
    }

    public void CompleteStop()
    {
        ExposedFinishStop();
        DrainAll();
    }

    public void StartAndComplete()
    {
        Start();
        DrainAll();
        CompleteStart();
    }

    public void StopAndComplete(StopReason reason, bool tryBeGentle)
    {
        Stop(reason, tryBeGentle);
        DrainAll();
        CompleteStop();
    }

    #endregion

    #region Recorded hooks

    public int BeginStartCalls { get; private set; }

    public int BeginStopCalls { get; private set; }

    public StopReason? LastStopReason { get; private set; }

    public bool? LastTryBeGentle { get; private set; }

    /// <summary>
    /// Gets the clones of the buffers passed to <see cref="DoSendRaw"/>, in admission order.
    /// </summary>
    public IReadOnlyList<UnionDataList> CapturedSends => _capturedSends;

    #endregion

    #region Exposed protected surface

    public string ExposedTransportName => TransportName;

    public ILogger ExposedLog => Log;

    public IMemoryRental ExposedMemory => Memory;

    public bool ExposedCanSendNow(out SendResult result) => CanSendNow(out result);

    public SendResult ExposedScheduleToSend(UnionDataList bufferToSend) => ScheduleToSend(bufferToSend);

    public void ExposedProcessInbound(UnionDataList message) => ProcessInboundMessage(message);

    public void ExposedFailHard(StopReason reason) => FailHard(reason);

    public void ExposedFinishStart(Action onFail) => FinishStart(onFail);

    public void ExposedFinishStop() => FinishStop();

    #endregion

    protected override void BeginStart()
    {
        BeginStartCalls++;
    }

    protected override void BeginStop(StopReason reason, bool tryBeGentle)
    {
        BeginStopCalls++;
        LastStopReason = reason;
        LastTryBeGentle = tryBeGentle;
    }

    protected override void DoSendRaw(UnionDataList bufferToSend)
    {
        _capturedSends.Add(bufferToSend.Clone(Memory.CollectablePool));
        bufferToSend.Release();
    }

    public void Dispose()
    {
        foreach (UnionDataList buffer in _capturedSends)
        {
            buffer.Release();
        }

        _capturedSends.Clear();
    }

    private static void NoOp()
    {
    }
}
