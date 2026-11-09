using System;
using System.Collections.Generic;
using Actuarius.Memory;
using Pontifex.Endpoints;
using Pontifex.Raw.Reliable;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Tests.Endpoints;

/// <summary>
/// Test double for <see cref="RawReliableClientEndpoint"/>. It exposes the protected surface, records the
/// transport hooks, and drives completion explicitly: <see cref="BeginConnect"/> and
/// <see cref="BeginDisconnect"/> only record the call; tests complete them through
/// <see cref="CompleteConnect"/> / <see cref="CompleteDisconnect"/>.
/// </summary>
internal sealed class TestRawReliableClientEndpoint : RawReliableClientEndpoint, IDisposable
{
    // No initializer: the queue is created during the base constructor, before field initializers run.
    private ManualSerialActionQueue? _queue;
    private readonly List<UnionDataList> _capturedSends = new();

    private DateTime _now = new(2000, 1, 1);

    public TestRawReliableClientEndpoint(
        IRawReliableClientHandler handler,
        IEndPoint? remoteEndPoint = null,
        string transportName = "TestClientTransport",
        int messageMaxByteSize = 1024,
        long bufferCapacityBytes = 1024 * 1024,
        IMemoryRental? memory = null,
        ILogger? log = null,
        TimeSpan? pingPeriod = null,
        TimeSpan? disconnectTimeout = null)
        : base(
            transportName,
            remoteEndPoint ?? VoidEndPoint.Instance,
            handler,
            messageMaxByteSize,
            bufferCapacityBytes,
            memory ?? MemoryRental.Shared,
            log ?? new Logger([]),
            pingPeriod != null || disconnectTimeout != null
                ? new PingOptions(disconnectTimeout ?? TimeSpan.Zero, pingPeriod ?? TimeSpan.Zero)
                : (PingOptions?)null)
    {
    }

    protected override ISerialActionQueue SerialQueueConstructor(Action<Exception> onError)
    {
        return _queue = new ManualSerialActionQueue(onError);
    }

    protected override DateTime Now => _now;

    protected override void StartKeepAlive()
    {
        // The background loop is suppressed so tests can drive KeepAliveTick deterministically.
    }

    #region Clock control

    public void AdvanceNow(TimeSpan delta) => _now += delta;

    #endregion

    /// <summary>
    /// The frame type bytes, exposed so tests can build and assert framed messages without naming
    /// the protected nested <c>MessageType</c> enum.
    /// </summary>
    public const byte InvalidFrame = (byte)MessageType.InvalidMessage;
    public const byte PayloadFrame = (byte)MessageType.PayloadMessage;
    public const byte DisconnectFrame = (byte)MessageType.DisconnectMessage;
    public const byte PingFrame = (byte)MessageType.PingMessage;
    public const byte PongFrame = (byte)MessageType.PongMessage;
    public const byte AckRequestFrame = (byte)MessageType.AckRequestMessage;
    public const byte AckResponseFrame = (byte)MessageType.AckResponseMessage;

    #region Queue and lifecycle control

    public void DrainAll() => _queue!.DrainAll();

    private void CompleteConnect(Action? ifFailed = null)
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

    /// <summary>
    /// Stops the endpoint and completes the disconnect when the endpoint was connected. The
    /// terminal stopped event is raised by <see cref="CompleteDisconnect"/>.
    /// </summary>
    public void StopAndComplete(StopReason reason, bool tryBeGentle)
    {
        int disconnectsBefore = BeginDisconnectCalls;

        Stop(reason, tryBeGentle);
        DrainAll();

        if (BeginDisconnectCalls > disconnectsBefore)
        {
            CompleteDisconnect();
        }
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

    public bool ExposedKeepAliveTick() => KeepAliveTick();

    #endregion

    protected override void BeginConnect()
    {
    }

    protected override void BeginDisconnect(StopReason reason, bool tryBeGentle)
    {
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

    #region Frame builders

    public static UnionDataList Frame(IMemoryRental memory, byte rawType)
    {
        UnionDataList list = memory.CollectablePool.Acquire<UnionDataList>();
        list.PutFirst(new UnionData(rawType));
        return list;
    }

    public static UnionDataList Frame(IMemoryRental memory, byte rawType, byte payload)
    {
        UnionDataList list = memory.CollectablePool.Acquire<UnionDataList>();
        list.PutLast(new UnionData(payload));
        list.PutFirst(new UnionData(rawType));
        return list;
    }

    #endregion

    private static void NoOp()
    {
    }
}
