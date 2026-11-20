using System;
using System.Collections.Generic;
using Actuarius.Memory;
using Pontifex.Endpoints;
using Pontifex.Raw.Reliable;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Tests.Endpoints;

/// <summary>
/// Test double for <see cref="RawReliableHandshakeClientEndpoint"/>. It exposes the protected surface, records the
/// transport hooks, and drives completion explicitly: <see cref="BeginConnect"/> only records the call and
/// <see cref="BeginDisconnect"/> calls the reliable base implementation (raising the logical
/// disconnect notification) and then records the call. Keep-alive is disabled.
/// </summary>
internal sealed class TestRawReliableHandshakeClientEndpoint : RawReliableHandshakeClientEndpoint, IDisposable
{
    // No initializer: the queue is created during the base constructor, before field initializers run.
    private ManualSerialActionQueue? _queue;
    private readonly List<UnionDataList> _capturedSends = new();

    public TestRawReliableHandshakeClientEndpoint(
        IRawReliableClientHandler handler,
        IEndPoint? remoteEndPoint = null,
        string transportName = "TestRawReliableHandshakeClientEndpoint",
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
            log ?? new Logger([]),
            null)
    {
    }

    protected override ISerialActionQueue SerialQueueConstructor(Action<Exception> onError)
    {
        return _queue = new ManualSerialActionQueue(onError);
    }

    #region Queue and lifecycle control

    public void DrainAll() => _queue!.DrainAll();

    public void CompleteConnect(Action? ifFailed = null)
    {
        FinishConnect(ifFailed ?? NoOp);
        DrainAll();
    }

    public void CompleteDisconnect()
    {
        FinishDisconnect();
        DrainAll();
    }

    public void ConnectAndComplete()
    {
        Start();
        DrainAll();
        CompleteConnect();
    }

    #endregion

    #region Recorded hooks

    public int BeginDisconnectCalls { get; private set; }

    public StopReason? LastDisconnectReason { get; private set; }

    /// <summary>
    /// Gets clones of the buffers passed to <see cref="DoSendRaw"/>, in admission order.
    /// </summary>
    public IReadOnlyList<UnionDataList> CapturedSends => _capturedSends;

    #endregion

    #region Exposed protected surface

    public void ExposedProcessInbound(UnionDataList framedMessage) => ProcessInboundMessage(framedMessage);

    public void ExposedFailHard(StopReason reason) => FailHard(reason);

    #endregion

    protected override void BeginConnect()
    {
    }

    protected override void BeginDisconnect(StopReason reason, bool tryBeGentle)
    {
        base.BeginDisconnect(reason, tryBeGentle);
        BeginDisconnectCalls++;
        LastDisconnectReason = reason;
    }

    protected override void OnConnected()
    {
        base.OnConnected();
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
