using System;
using Actuarius.Memory;
using Pontifex.RR.Unreliable;
using Pontifex.StopReasons;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Tests.Transports;

/// <summary>
/// Concrete carrier-free unreliable RR client used by the client layer.
/// </summary>
internal sealed class TestRRUnreliableClientTransport : RRUnreliableClientTransport
{
    public TestRRUnreliableClientTransport(IEndPoint? serverEndPoint, TimeSpan requestTtl,
        ManualClock clock, int messageMaxByteSize = 64)
        : base("test-rr-unreliable-client", serverEndPoint!, requestTtl, new Logger([]),
            MemoryRental.Shared, clock)
    {
        ServerRoute = serverEndPoint!;
        MessageLimit = messageMaxByteSize;
    }

    public IEndPoint ServerRoute { get; }

    public int MessageLimit { get; }

    public override int MessageMaxByteSize => MessageLimit;

    public SendResult SendResult { get; set; } = SendResult.Ok;

    public bool ThrowOnSend { get; set; }

    public Action? OnSend { get; set; }

    public int SendCalls { get; private set; }

    public int StopCarrierCalls { get; private set; }

    public int PendingCount => PendingCountForTests;

    public Action? BeforeReapExpired
    {
        get => BeforeReapExpiredForTests;
        set => BeforeReapExpiredForTests = value;
    }

    public Action<ushort>? AfterAddBeforeBind
    {
        get => AfterAddBeforeBindForTests;
        set => AfterAddBeforeBindForTests = value;
    }

    public Action? BeforeSend
    {
        get => BeforeSendForTests;
        set => BeforeSendForTests = value;
    }

    public void Receive(UnionDataList frame)
    {
        OnFrameReceived(ServerRoute, frame);
    }

    public void ReceiveFrom(IEndPoint source, UnionDataList frame)
    {
        OnFrameReceived(source, frame);
    }

    protected override bool StartCarrier()
    {
        return true;
    }

    protected override void StopCarrier(StopReason reason)
    {
        StopCarrierCalls++;
    }

    protected override SendResult SendFrame(UnionDataList frame)
    {
        SendCalls++;

        try
        {
            OnSend?.Invoke();
        }
        finally
        {
            frame.Release();
        }

        if (ThrowOnSend)
        {
            throw new InvalidOperationException("client send failure");
        }

        return SendResult;
    }
}
