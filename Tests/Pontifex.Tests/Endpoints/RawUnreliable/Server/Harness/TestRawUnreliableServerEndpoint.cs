using System;
using System.Collections.Generic;
using Actuarius.Memory;
using Pontifex.Endpoints;
using Pontifex.Raw.Unreliable;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Tests.Endpoints;

/// <summary>
/// Test double for <see cref="RawUnreliableServerEndpoint"/>. It supplies the pending trigger and the shared
/// <see cref="SendBudget"/>, records the outbound buffers passed to the carrier hook, and records the terminal
/// <c>Stopped</c> notifications. The start is driven through the inherited implementation.
/// </summary>
internal sealed class TestRawUnreliableServerEndpoint : RawUnreliableServerEndpoint, IDisposable
{
    // No initializer: the queue is created during the base constructor, before field initializers run.
    private ManualSerialActionQueue? _queue;
    private readonly List<UnionDataList> _capturedSends = new();
    private readonly List<StopReason> _stoppedReasons = new();

    public TestRawUnreliableServerEndpoint(
        IRawUnreliableHandler handler,
        UnionDataList trigger,
        SendBudget sendBudget,
        IEndPoint? remoteEndPoint = null,
        string transportName = "TestServerTransport",
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
            trigger,
            sendBudget,
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
    /// Runs exactly one queued action so a test can interleave scheduling and execution. Does nothing
    /// when no action is pending.
    /// </summary>
    public void DrainOne() => _queue!.DrainOne();

    #endregion

    #region Lifecycle helpers

    public void StartAndComplete()
    {
        Start();
        DrainAll();
    }

    public bool StopOneShot(StopReason? reason = null) => ((IRawUnreliableEndpoint)this).Stop(reason);

    #endregion

    #region Recorded hooks

    public IReadOnlyList<StopReason> StoppedReasons => _stoppedReasons;

    /// <summary>
    /// Gets clones of the buffers passed to <see cref="RawUnreliableEndpoint.DoSendRaw"/>, in admission
    /// order.
    /// </summary>
    public IReadOnlyList<UnionDataList> CapturedSends => _capturedSends;

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
