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
    private readonly List<(int DeltaBytes, int DeltaMessages)> _pendingSendChanges = new();

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

    public void DrainAll() => _queue!.DrainAll();

    /// <summary>
    /// Runs exactly one queued action so a test can interleave scheduling and execution. Does nothing
    /// when no action is pending.
    /// </summary>
    public void DrainOne() => _queue!.DrainOne();

    /// <summary>
    /// Makes the next action submission fail, modelling a queue that rejects an admission. The flag is
    /// consumed by that call and is off by default.
    /// </summary>
    public void RejectNextSchedule() => _queue!.RejectNextSchedule();

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

    /// <summary>
    /// Gets the pending-send deltas reported through <see cref="OnPendingSendChanged"/>, in call order.
    /// </summary>
    public IReadOnlyList<(int DeltaBytes, int DeltaMessages)> PendingSendChanges => _pendingSendChanges;

    /// <summary>
    /// Gets the sum of the reported byte deltas: the endpoint's current pending outbound reservation.
    /// </summary>
    public int PendingSendBytes { get; private set; }

    /// <summary>
    /// Gets the sum of the reported message deltas: the endpoint's current pending message count.
    /// </summary>
    public int PendingSendMessages { get; private set; }

    /// <summary>
    /// Gets or sets a value indicating whether <see cref="OnPendingSendChanged"/> throws after recording
    /// the delta, modelling an override that violates its contract.
    /// </summary>
    public bool ThrowOnPendingSendChanged { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether <see cref="DoSendRaw"/> throws instead of transmitting,
    /// modelling a carrier hook that violates its contract.
    /// </summary>
    public bool ThrowOnSend { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether an admission passes <see cref="CanSendNow"/> while a stop
    /// has already been requested. It models a send that observed the flow-control snapshot just before
    /// the stop latch, so its queued action lands behind the stop transition.
    /// </summary>
    public bool AdmitWhileStopping { get; set; }

    #endregion

    #region Exposed protected surface

    public bool ExposedCanSendNow(out SendResult result) => CanSendNow(out result);

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

    protected override bool CanSendNow(out SendResult errorCode)
    {
        if (AdmitWhileStopping)
        {
            errorCode = SendResult.Ok;
            return true;
        }

        return base.CanSendNow(out errorCode);
    }

    protected override void OnPendingSendChanged(int deltaBytes, int deltaMessages)
    {
        _pendingSendChanges.Add((deltaBytes, deltaMessages));
        PendingSendBytes += deltaBytes;
        PendingSendMessages += deltaMessages;

        if (ThrowOnPendingSendChanged)
        {
            throw new InvalidOperationException("OnPendingSendChanged override failed");
        }
    }

    protected override void DoSendRaw(UnionDataList bufferToSend)
    {
        if (ThrowOnSend)
        {
            throw new InvalidOperationException("DoSendRaw failed");
        }

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
