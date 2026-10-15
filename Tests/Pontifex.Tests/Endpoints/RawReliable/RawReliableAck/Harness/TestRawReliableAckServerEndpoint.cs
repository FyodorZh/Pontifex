using System;
using System.Collections.Generic;
using Actuarius.Memory;
using Pontifex.Endpoints;
using Pontifex.Raw.Reliable;
using Pontifex.Raw.Reliable.Ack;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Tests.Endpoints;

/// <summary>
/// Test double for <see cref="RawReliableAckServerEndpoint"/>. It exposes the protected surface, records the
/// transport hooks, and drives completion explicitly: <see cref="BeginConnect"/> only records the call and
/// <see cref="BeginDisconnect"/> calls the acknowledgement base implementation (raising the logical
/// disconnect notification) and then records the call. It also overrides the <see cref="Now"/> seam so the
/// idle-session watchdog can be advanced deterministically.
/// </summary>
internal sealed class TestRawReliableAckServerEndpoint : RawReliableAckServerEndpoint, IDisposable
{
    // No initializer: the queue is created during the base constructor, before field initializers run.
    private ManualSerialActionQueue? _queue;
    private readonly List<UnionDataList> _capturedSends = new();

    private DateTime _now = new(2000, 1, 1);

    public TestRawReliableAckServerEndpoint(
        IRawReliableAckServerHandler handler,
        IEndPoint? remoteEndPoint = null,
        string transportName = "TestAckServerTransport",
        int messageMaxByteSize = 1024,
        long bufferCapacityBytes = 1024 * 1024,
        IMemoryRental? memory = null,
        ILogger? log = null,
        TimeSpan? disconnectTimeout = null)
        : base(
            transportName,
            remoteEndPoint ?? VoidEndPoint.Instance,
            handler,
            messageMaxByteSize,
            bufferCapacityBytes,
            memory ?? MemoryRental.Shared,
            log ?? new Logger([]),
            disconnectTimeout ?? TimeSpan.FromSeconds(30))
    {
    }

    protected override ISerialActionQueue SerialQueueConstructor(Action<Exception> onError)
    {
        return _queue = new ManualSerialActionQueue(onError);
    }

    protected override DateTime Now => _now;

    #region Clock control

    public void SetNow(DateTime value) => _now = value;

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

    public void DrainOne() => _queue!.DrainOne();

    public void DrainAll() => _queue!.DrainAll();

    public void CompleteConnect(Action? ifFailed = null)
    {
        ExposedFinishConnect(ifFailed ?? NoOp);
        DrainAll();
    }

    public void CompleteDisconnect()
    {
        ExposedFinishDisconnect();
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

    public int BeginConnectCalls { get; private set; }

    public int BeginDisconnectCalls { get; private set; }

    public StopReason? LastDisconnectReason { get; private set; }

    public bool? LastTryBeGentle { get; private set; }

    public int OnConnectedCalls { get; private set; }

    /// <summary>
    /// Gets clones of the buffers passed to <see cref="DoSendRaw"/>, in admission order.
    /// </summary>
    public IReadOnlyList<UnionDataList> CapturedSends => _capturedSends;

    #endregion

    #region Exposed protected surface

    public string ExposedTransportName => TransportName;

    public ILogger ExposedLog => Log;

    public IMemoryRental ExposedMemory => Memory;

    public bool ExposedCanSendNow(out SendResult result) => CanSendNow(out result);

    public void ExposedFinishConnect(Action ifFailed) => FinishConnect(ifFailed);

    public void ExposedFinishDisconnect() => FinishDisconnect();

    public void ExposedProcessInbound(UnionDataList framedMessage) => ProcessInboundMessage(framedMessage);

    public bool ExposedDispatch(byte rawType, UnionDataList message) => ProcessInboundMessage((MessageType)rawType, message);

    public void ExposedFailHard(StopReason reason) => FailHard(reason);

    #endregion

    protected override void BeginConnect()
    {
        BeginConnectCalls++;
    }

    protected override void BeginDisconnect(StopReason reason, bool tryBeGentle)
    {
        base.BeginDisconnect(reason, tryBeGentle);
        BeginDisconnectCalls++;
        LastDisconnectReason = reason;
        LastTryBeGentle = tryBeGentle;
    }

    protected override void OnConnected()
    {
        OnConnectedCalls++;
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
