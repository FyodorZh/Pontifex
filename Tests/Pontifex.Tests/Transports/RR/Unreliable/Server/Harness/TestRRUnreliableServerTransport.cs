using System;
using System.Collections.Generic;
using System.Threading;
using Actuarius.Memory;
using Pontifex.Endpoints;
using Pontifex.RR.Unreliable;
using Pontifex.StopReasons;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Tests.Transports;

/// <summary>
/// One response frame captured by <see cref="TestRRUnreliableServerTransport"/> before it releases the
/// frame.
/// </summary>
internal sealed class RecordedUnreliableFrame
{
    public RecordedUnreliableFrame(IEndPoint destination, bool decoded, ushort requestId,
        int messageId, int messageCount, int payloadValue)
    {
        Destination = destination;
        Decoded = decoded;
        RequestId = requestId;
        MessageId = messageId;
        MessageCount = messageCount;
        PayloadValue = payloadValue;
    }

    public IEndPoint Destination { get; }

    public bool Decoded { get; }

    public ushort RequestId { get; }

    public int MessageId { get; }

    public int MessageCount { get; }

    public int PayloadValue { get; }
}

/// <summary>
/// Concrete carrier-free unreliable RR server used by the server layer. It records every outbound
/// frame and can block, fail, or throw the first send.
/// </summary>
internal sealed class TestRRUnreliableServerTransport : RRUnreliableServerTransport
{
    private readonly object _sentLocker = new object();
    private readonly List<RecordedUnreliableFrame> _sentFrames = new List<RecordedUnreliableFrame>();
    private int _sendCalls;
    private int _activeSends;
    private int _maxActiveSends;

    public TestRRUnreliableServerTransport(TimeSpan responseTtl, ManualClock clock,
        ILogger? logger = null, int messageMaxByteSize = 64)
        : base("test-rr-unreliable-server", responseTtl, logger ?? new Logger([]),
            MemoryRental.Shared, clock)
    {
        Clock = clock;
        MessageLimit = messageMaxByteSize;
    }

    public ManualClock Clock { get; }

    public int MessageLimit { get; }

    public override int MessageMaxByteSize => MessageLimit;

    public SendResult SendResult { get; set; } = SendResult.Ok;

    public bool ThrowOnSend { get; set; }

    public bool BlockFirstSend { get; set; }

    public ManualResetEventSlim FirstSendEntered { get; } = new ManualResetEventSlim(false);

    public ManualResetEventSlim ReleaseFirstSend { get; } = new ManualResetEventSlim(false);

    public int SendCalls => Volatile.Read(ref _sendCalls);

    public int ActiveSends => Volatile.Read(ref _activeSends);

    public int MaxActiveSends => Volatile.Read(ref _maxActiveSends);

    public RecordedUnreliableFrame[] SentFrames
    {
        get
        {
            lock (_sentLocker)
            {
                return _sentFrames.ToArray();
            }
        }
    }

    public RecordedUnreliableFrame? LastSent
    {
        get
        {
            lock (_sentLocker)
            {
                return _sentFrames.Count == 0 ? null : _sentFrames[_sentFrames.Count - 1];
            }
        }
    }

    public static StringEndPoint NewClientRoute(string name = "rr-unreliable-client")
    {
        return new StringEndPoint(name);
    }

    public void Receive(IEndPoint source, UnionDataList frame)
    {
        OnFrameReceived(source, frame);
    }

    protected override bool StartCarrier()
    {
        return true;
    }

    protected override void StopCarrier(StopReason reason)
    {
    }

    protected override SendResult SendFrame(IEndPoint destination, UnionDataList frame)
    {
        int active = Interlocked.Increment(ref _activeSends);
        UpdateMax(active);

        try
        {
            bool decoded = RRUnreliableFrame.TryDecodeResponse(frame, out ushort requestId,
                out int messageId, out int messageCount);
            int payloadValue = frame.Elements.Count > 0
                ? frame.Elements[0].Alias.IntValue
                : int.MinValue;

            var recorded = new RecordedUnreliableFrame(destination, decoded, requestId, messageId,
                messageCount, payloadValue);

            lock (_sentLocker)
            {
                _sentFrames.Add(recorded);
            }

            Interlocked.Increment(ref _sendCalls);

            if (BlockFirstSend && messageId == 0)
            {
                FirstSendEntered.Set();
                ReleaseFirstSend.Wait(TimeSpan.FromSeconds(5));
            }

            if (ThrowOnSend)
            {
                throw new InvalidOperationException("server send failure");
            }

            return SendResult;
        }
        finally
        {
            frame.Release();
            Interlocked.Decrement(ref _activeSends);
        }
    }

    private void UpdateMax(int active)
    {
        int max = Volatile.Read(ref _maxActiveSends);

        while (active > max)
        {
            int previous = Interlocked.CompareExchange(ref _maxActiveSends, active, max);

            if (previous == max)
            {
                return;
            }

            max = previous;
        }
    }
}
