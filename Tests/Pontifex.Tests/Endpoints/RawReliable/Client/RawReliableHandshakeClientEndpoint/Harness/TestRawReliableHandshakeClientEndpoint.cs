using System;
using System.Collections.Generic;
using System.Text;
using Actuarius.Memory;
using Pontifex.Endpoints;
using Pontifex.Raw.Reliable;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Tests.Endpoints;

/// <summary>
/// Test double for <see cref="RawReliableHandshakeClientEndpoint"/>. It exposes the protected surface, records
/// the transport hooks, and drives completion explicitly: <see cref="BeginConnect"/> only records the call and
/// <see cref="BeginDisconnect"/> calls the reliable base implementation (raising the logical disconnect
/// notification) and then records the call. The handshake deadline is driven by a manual timer. Keep-alive is
/// disabled.
/// </summary>
internal sealed class TestRawReliableHandshakeClientEndpoint : RawReliableHandshakeClientEndpoint, IDisposable
{
    // No initializer: the queue is created during the base constructor, before field initializers run.
    private ManualSerialActionQueue? _queue;
    private ManualHandshakeTimer? _handshakeTimer;
    private readonly List<UnionDataList> _capturedSends = new();
    private readonly string _handshakeName;
    private readonly int _messageMaxByteSize;

    public TestRawReliableHandshakeClientEndpoint(
        IRawReliableClientHandler handler,
        IEndPoint? remoteEndPoint = null,
        string transportName = "TestRawReliableHandshakeClientEndpoint",
        int messageMaxByteSize = 1024,
        long bufferCapacityBytes = 1024 * 1024,
        IMemoryRental? memory = null,
        ILogger? log = null,
        string handshakeName = "test-handshake",
        int handshakeMaxByteSize = RawReliableHandshake.DefaultMaxByteSize,
        TimeSpan? handshakeTimeout = null)
        : base(
            transportName,
            remoteEndPoint ?? VoidEndPoint.Instance,
            handler,
            messageMaxByteSize,
            bufferCapacityBytes,
            memory ?? MemoryRental.Shared,
            log ?? new Logger([]),
            null,
            handshakeName,
            handshakeMaxByteSize,
            handshakeTimeout)
    {
        _handshakeName = handshakeName;
        _messageMaxByteSize = messageMaxByteSize;
    }

    protected override ISerialActionQueue SerialQueueConstructor(Action<Exception> onError)
    {
        return _queue = new ManualSerialActionQueue(onError);
    }

    protected override IDisposable? CreateHandshakeTimer(TimeSpan timeout, Action onTimeout)
    {
        return _handshakeTimer = new ManualHandshakeTimer(onTimeout);
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

    public void ConnectAndHandshake()
    {
        ConnectAndComplete();
        CompleteHandshake();
    }

    /// <summary>
    /// Feeds a valid handshake response through the inbound dispatch and drains the queue.
    /// </summary>
    public void CompleteHandshake(short? version = null, string? name = null, int? messageMaxByteSize = null)
    {
        var response = RawReliableHandshake.BuildResponse(
            version ?? RawReliableHandshake.Version,
            name ?? _handshakeName,
            messageMaxByteSize ?? _messageMaxByteSize);
        response.PutFirst((byte)MessageType.HandshakeResponseMessage);
        ExposedProcessInbound(response);
        DrainAll();
    }

    /// <summary>
    /// Feeds a handshake response with an arbitrary agreed maximum, including values below 1 that the
    /// production builder rejects.
    /// </summary>
    public void FeedHandshakeResponse(short version, string name, int messageMaxByteSize)
    {
        var response = MemoryRental.Shared.CollectablePool.Acquire<UnionDataList>();
        response.PutLast(new UnionData(version));
        response.PutLast(new UnionData(new MultiRefByteArray(Encoding.UTF8.GetBytes(name))));
        response.PutLast(new UnionData(messageMaxByteSize));
        FeedHandshakeResponsePayload(response);
    }

    /// <summary>
    /// Feeds an arbitrary payload as a handshake-response frame through the inbound dispatch.
    /// </summary>
    public void FeedHandshakeResponsePayload(UnionDataList payload)
    {
        payload.PutFirst((byte)MessageType.HandshakeResponseMessage);
        ExposedProcessInbound(payload);
        DrainAll();
    }

    /// <summary>
    /// Feeds a handshake rejection through the inbound dispatch and drains the queue.
    /// </summary>
    public void RejectHandshake(
        RawReliableHandshake.HandshakeStatus status,
        string? text = null,
        short? serverVersion = null,
        string? serverName = null)
    {
        var reject = RawReliableHandshake.BuildReject(
            serverVersion ?? RawReliableHandshake.Version,
            serverName ?? _handshakeName,
            status,
            text);
        reject.PutFirst((byte)MessageType.HandshakeRejectMessage);
        ExposedProcessInbound(reject);
        DrainAll();
    }

    /// <summary>
    /// Fires the manual handshake deadline once, as the production timer would.
    /// </summary>
    public void FireHandshakeTimer()
    {
        var timer = _handshakeTimer;
        if (timer == null)
        {
            throw new InvalidOperationException("The handshake timer was not created.");
        }

        timer.Fire();
    }

    public bool IsHandshakeTimerDisposed => _handshakeTimer?.IsDisposed ?? true;

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
