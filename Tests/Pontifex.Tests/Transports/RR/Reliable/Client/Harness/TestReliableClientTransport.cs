using System;
using System.Collections.Generic;
using System.Threading;
using Actuarius.Memory;
using Pontifex.RR;
using Pontifex.RR.Reliable;
using Pontifex.StopReasons;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Tests.Transports;

/// <summary>
/// Concrete carrier-free reliable RR client used by the client layer.
/// </summary>
internal sealed class TestReliableClientTransport : RRReliableClientTransport
{
    private readonly ScriptedReliableCarrier _carrier;

    public TestReliableClientTransport(ScriptedReliableCarrier carrier, TimeSpan requestTtl,
        ITimerFactory timerFactory, ushort randomSeed = 1, int messageMaxByteSize = 64)
        : base("test-reliable-client", carrier.ServerEndPoint, requestTtl, new Logger([]),
            MemoryRental.Shared, timerFactory)
    {
        _carrier = carrier;
        RandomSeed = randomSeed;
        MessageLimit = messageMaxByteSize;
    }

    public int MessageLimit { get; }

    public override int MessageMaxByteSize => MessageLimit;

    public ushort RandomSeed { get; set; }

    public Func<UnionDataList, SendResult>? SendOverride { get; set; }

    public int SendCalls { get; private set; }

    public SendResult? LastSendResult { get; private set; }

    public Action? OnBeforeSend { get; set; }

    public void Receive(UnionDataList frame)
    {
        OnFrameReceived(ServerEndPoint, frame);
    }

    public void ReceiveFromForeignRoute(IEndPoint source, UnionDataList frame)
    {
        OnFrameReceived(source, frame);
    }

    public Action? ExposeBeforeSendGate
    {
        get => BeforeSendGateForTests;
        set => BeforeSendGateForTests = value;
    }

    protected override bool StartCarrier()
    {
        return true;
    }

    protected override void StopCarrier(StopReason reason)
    {
    }

    protected override ushort CreateRandomRequestId()
    {
        return RandomSeed;
    }

    protected override SendResult SendFrame(UnionDataList frame)
    {
        SendCalls++;
        OnBeforeSend?.Invoke();

        SendResult result = SendOverride != null ? SendOverride(frame) : _carrier.SendFromClient(frame);
        LastSendResult = result;
        return result;
    }
}

/// <summary>
/// Client request handler that records every callback and can act from inside one.
/// </summary>
internal sealed class RecordingReliableClientHandler : RRRequestHandler, IRRReliableRequestHandler
{
    private readonly object _locker = new object();
    private readonly List<int> _responses = new List<int>();
    private readonly List<RRReliableCompletionType> _finishes = new List<RRReliableCompletionType>();

    public ManualResetEventSlim Responded { get; } = new ManualResetEventSlim(false);

    public ManualResetEventSlim Finished { get; } = new ManualResetEventSlim(false);

    public Action<UnionDataList>? OnResponse { get; set; }

    public Action<RRReliableCompletionType>? OnFinish { get; set; }

    public bool ThrowOnResponse { get; set; }

    public bool ThrowOnFinish { get; set; }

    public int ResponseCount
    {
        get
        {
            lock (_locker)
            {
                return _responses.Count;
            }
        }
    }

    public int FinishCount
    {
        get
        {
            lock (_locker)
            {
                return _finishes.Count;
            }
        }
    }

    public int[] ResponseValues
    {
        get
        {
            lock (_locker)
            {
                return _responses.ToArray();
            }
        }
    }

    public RRReliableCompletionType[] Finishes
    {
        get
        {
            lock (_locker)
            {
                return _finishes.ToArray();
            }
        }
    }

    public void Response(UnionDataList response)
    {
        try
        {
            int value = response.Elements.Count > 0 ? response.Elements[0].Alias.IntValue : int.MinValue;

            lock (_locker)
            {
                _responses.Add(value);
            }

            Responded.Set();

            Action<UnionDataList>? action = OnResponse;
            action?.Invoke(response);

            if (ThrowOnResponse)
            {
                throw new InvalidOperationException("client response handler failure");
            }
        }
        finally
        {
            response.Release();
        }
    }

    public void Finish(RRReliableCompletionType type)
    {
        lock (_locker)
        {
            _finishes.Add(type);
        }

        OnFinish?.Invoke(type);

        Finished.Set();

        if (ThrowOnFinish)
        {
            throw new InvalidOperationException("client finish handler failure");
        }
    }

    public void ResetSignals()
    {
        Responded.Reset();
        Finished.Reset();
    }
}
