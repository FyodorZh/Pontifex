using System;
using Actuarius.Memory;
using Pontifex.Endpoints;
using Pontifex.Raw.Reliable;
using Pontifex.StopReasons;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Tests.Transports;

/// <summary>
/// Test double for a server-session endpoint used by the server transport tests. The serial queue is
/// scripted so a test can choose whether actions run immediately or on an explicit drain, and whether
/// the next scheduling call is rejected or throws. <see cref="BeginConnect"/> completes the connection
/// automatically unless a test replaces that behavior, and <see cref="BeginDisconnect"/> completes the
/// disconnection automatically.
/// </summary>
internal sealed class TestRawReliableServerSession : RawReliableServerEndpoint
{
    // No initializer: the queue is created during the base constructor, before field initializers run.
    private ScriptedSerialActionQueue? _queue;

    private DateTime _now = new(2000, 1, 1);

    public TestRawReliableServerSession(
        IRawReliableServerHandler handler,
        string transportName = "TestServerSession",
        int messageMaxByteSize = 1024,
        long bufferCapacityBytes = 1024 * 1024,
        IMemoryRental? memory = null,
        ILogger? log = null,
        TimeSpan? disconnectTimeout = null)
        : base(
            transportName,
            VoidEndPoint.Instance,
            handler,
            messageMaxByteSize,
            bufferCapacityBytes,
            memory ?? MemoryRental.Shared,
            log ?? new Logger([]),
            disconnectTimeout ?? TimeSpan.FromSeconds(30))
    {
    }

    public ScriptedSerialActionQueue Queue => _queue!;

    public int BeginConnectCalls { get; private set; }

    public int BeginDisconnectCalls { get; private set; }

    public StopReason? LastDisconnectReason { get; private set; }

    public bool? LastTryBeGentle { get; private set; }

    public int OnConnectedCalls { get; private set; }

    /// <summary>
    /// Gets the number of times the <see cref="Now"/> seam was read.
    /// </summary>
    public int NowReadCount { get; private set; }

    /// <summary>
    /// Gets or sets an exception thrown from <see cref="Now"/>. Used to verify that the shared ticker
    /// isolates a failing session.
    /// </summary>
    public Exception? NowException { get; set; }

    /// <summary>
    /// Gets or sets an action invoked at the start of <see cref="BeginConnect"/>.
    /// </summary>
    public Action<TestRawReliableServerSession>? BeginConnectBehavior { get; set; }

    /// <summary>
    /// Gets or sets whether <see cref="BeginConnect"/> completes the connection automatically.
    /// </summary>
    public bool AutoCompleteConnect { get; set; } = true;

    /// <summary>
    /// Gets or sets whether <see cref="BeginDisconnect"/> completes the disconnection automatically.
    /// </summary>
    public bool AutoCompleteDisconnect { get; set; } = true;

    public void DrainOne() => _queue!.DrainOne();

    public void DrainAll() => _queue!.DrainAll();

    protected override ISerialActionQueue SerialQueueConstructor(Action<Exception> onError)
    {
        return _queue = new ScriptedSerialActionQueue(onError);
    }

    protected override DateTime Now
    {
        get
        {
            NowReadCount++;

            if (NowException != null)
            {
                throw NowException;
            }

            return _now;
        }
    }

    protected override void BeginConnect()
    {
        BeginConnectCalls++;
        BeginConnectBehavior?.Invoke(this);

        if (AutoCompleteConnect)
        {
            FinishConnect(NoOp);
        }
    }

    protected override void BeginDisconnect(StopReason reason, bool tryBeGentle)
    {
        BeginDisconnectCalls++;
        LastDisconnectReason = reason;
        LastTryBeGentle = tryBeGentle;

        if (AutoCompleteDisconnect)
        {
            FinishDisconnect();
        }
    }

    protected override void OnConnected()
    {
        OnConnectedCalls++;
        base.OnConnected();
    }

    protected override void DoSendRaw(UnionDataList bufferToSend)
    {
        bufferToSend.Release();
    }

    private static void NoOp()
    {
    }
}
