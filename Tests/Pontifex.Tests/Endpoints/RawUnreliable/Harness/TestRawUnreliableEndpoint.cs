using System;
using System.Collections.Generic;
using Actuarius.Memory;
using Pontifex.Endpoints;
using Pontifex.Raw.Unreliable;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Tests.Endpoints;

/// <summary>
/// Test double for <see cref="RawUnreliableEndpoint"/>. It exposes the protected surface of the endpoint,
/// records the transport hooks, and records the terminal <see cref="Pontifex.Raw.IRawEndpointImp.Stopped"/>
/// notifications. The start is driven through the inherited implementation, which schedules the finish-start
/// action and the inbox flush; <see cref="DrainOne"/> lets a test interleave a delivery between them.
/// </summary>
internal sealed class TestRawUnreliableEndpoint : RawUnreliableEndpoint, IDisposable
{
    // No initializer: the queue is created during the base constructor, before field initializers run.
    private ManualSerialActionQueue? _queue;
    private readonly List<UnionDataList> _capturedSends = new();
    private readonly List<StopReason> _stoppedReasons = new();

    public TestRawUnreliableEndpoint(
        IRawUnreliableHandler handler,
        IEndPoint? remoteEndPoint = null,
        string transportName = "TestTransport",
        int messageMaxByteSize = 1024,
        long bufferCapacityBytes = 1024 * 1024,
        int inboxCapacityMessages = 16,
        long inboxCapacityBytes = 1024 * 1024,
        IMemoryRental? memory = null,
        ILogger? log = null)
        : base(
            transportName,
            remoteEndPoint ?? VoidEndPoint.Instance,
            handler,
            messageMaxByteSize,
            bufferCapacityBytes,
            inboxCapacityMessages,
            inboxCapacityBytes,
            memory ?? MemoryRental.Shared,
            log ?? new Logger([]))
    {
        // Subscribed after the base subscription, so the inbox release always runs before this record.
        Stopped += _stoppedReasons.Add;
    }

    protected override ISerialActionQueue SerialQueueConstructor(Action<Exception> onError)
    {
        return _queue = new ManualSerialActionQueue(onError);
    }

    #region Queue control

    public void DrainAll() => _queue!.DrainAll();

    /// <summary>
    /// Gets the number of actions the endpoint has scheduled but the test has not run yet.
    /// </summary>
    public int PendingActions => _queue!.PendingCount;

    /// <summary>
    /// Runs exactly one queued action so a test can interleave scheduling and execution. Does nothing
    /// when no action is pending.
    /// </summary>
    public void DrainOne() => _queue!.DrainOne();

    #endregion

    #region Lifecycle helpers

    /// <summary>
    /// Starts the endpoint and drains the start, finish-start, and flush actions.
    /// </summary>
    public void StartAndComplete()
    {
        Start();
        DrainAll();
    }

    /// <summary>
    /// Invokes the one-shot <see cref="IRawUnreliableEndpoint.Stop"/> entry explicitly, exactly as an
    /// external caller or a transport stop cascade would.
    /// </summary>
    public bool StopOneShot(StopReason? reason = null) => ((IRawUnreliableEndpoint)this).Stop(reason);

    #endregion

    #region Recorded hooks

    /// <summary>
    /// Gets the reasons observed through the terminal <c>Stopped</c> event, in invocation order.
    /// </summary>
    public IReadOnlyList<StopReason> StoppedReasons => _stoppedReasons;

    /// <summary>
    /// Gets clones of the buffers passed to <see cref="RawUnreliableEndpoint.DoSendRaw"/>, in admission
    /// order.
    /// </summary>
    public IReadOnlyList<UnionDataList> CapturedSends => _capturedSends;

    #endregion

    #region Exposed protected surface

    public bool ExposedCanSendNow(out SendResult result) => CanSendNow(out result);

    public void ExposedFailHard(StopReason reason) => FailHard(reason);

    #endregion

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
}
