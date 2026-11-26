using System;
using System.Collections.Generic;
using System.Threading;
using Actuarius.Memory;
using Pontifex.RR.Reliable;
using Pontifex.StopReasons;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Tests.Transports;

/// <summary>
/// Concrete carrier-free reliable RR server used by the server layer.
/// </summary>
internal sealed class TestReliableServerTransport : RRReliableServerTransport
{
    private readonly ScriptedReliableCarrier _carrier;

    public TestReliableServerTransport(ScriptedReliableCarrier carrier, TimeSpan responseTtl,
        ITimerFactory timerFactory, int dispatchCapacity = 1024,
        int messageMaxByteSize = 64)
        : base("test-reliable-server", responseTtl, new Logger([]), MemoryRental.Shared,
            timerFactory, dispatchCapacity)
    {
        _carrier = carrier;
        MessageLimit = messageMaxByteSize;
    }

    public int MessageLimit { get; }

    public override int MessageMaxByteSize => MessageLimit;

    public Func<IEndPoint, UnionDataList, SendResult>? SendOverride { get; set; }

    public int SendCalls { get; private set; }

    public SendResult? LastSendResult { get; private set; }

    public bool StartDispatchForTests()
    {
        return StartDispatch();
    }

    public void StopDispatchForTests()
    {
        StopDispatch();
    }

    public void ReceiveFromClient(IEndPoint source, UnionDataList frame)
    {
        OnFrameReceived(source, frame);
    }

    public void CloseSession(IEndPoint source)
    {
        OnSessionClosed(source);
    }

    protected override bool StartCarrier()
    {
        return StartDispatch();
    }

    protected override void StopCarrier(StopReason reason)
    {
        StopDispatch();
    }

    protected override SendResult SendFrame(IEndPoint destination, UnionDataList frame)
    {
        SendCalls++;
        SendResult result = SendOverride != null
            ? SendOverride(destination, frame)
            : _carrier.SendFromServer(destination, frame);
        LastSendResult = result;
        return result;
    }
}

internal enum ServerHandlerMode
{
    Respond,
    RespondAndFinish,
    AcceptSilently,
    Reject,
    Throw
}

/// <summary>
/// Server handler that records the presented requests and can answer, reject, block, or throw.
/// </summary>
internal sealed class RecordingReliableServerHandler : IRRReliableServerHandler
{
    private readonly object _locker = new object();
    private readonly List<int> _requestValues = new List<int>();
    private readonly List<IEndPoint> _requestSources = new List<IEndPoint>();
    private readonly List<IRRReliableResponse> _handles = new List<IRRReliableResponse>();
    private int _activeCount;
    private int _maxActiveCount;

    public RecordingReliableServerHandler(ServerHandlerMode mode = ServerHandlerMode.AcceptSilently)
    {
        Mode = mode;
    }

    public ServerHandlerMode Mode { get; set; }

    public ManualResetEventSlim Requested { get; } = new ManualResetEventSlim(false);

    public ManualResetEventSlim? Entered { get; set; }

    public ManualResetEventSlim? Release { get; set; }

    public int RequestCount
    {
        get
        {
            lock (_locker)
            {
                return _requestValues.Count;
            }
        }
    }

    public int MaxActiveCount => Volatile.Read(ref _maxActiveCount);

    public int[] RequestValues
    {
        get
        {
            lock (_locker)
            {
                return _requestValues.ToArray();
            }
        }
    }

    public IEndPoint[] RequestSources
    {
        get
        {
            lock (_locker)
            {
                return _requestSources.ToArray();
            }
        }
    }

    public IRRReliableResponse[] Handles
    {
        get
        {
            lock (_locker)
            {
                return _handles.ToArray();
            }
        }
    }

    public IRRReliableResponse? LastHandle
    {
        get
        {
            lock (_locker)
            {
                return _handles.Count == 0 ? null : _handles[_handles.Count - 1];
            }
        }
    }

    public void OnRequested(IEndPoint client, UnionDataList request, IRRReliableResponse response)
    {
        int active = Interlocked.Increment(ref _activeCount);
        UpdateMax(active);

        try
        {
            int value = request.Elements.Count > 0 ? request.Elements[0].Alias.IntValue : int.MinValue;

            lock (_locker)
            {
                _requestValues.Add(value);
                _requestSources.Add(client);
                _handles.Add(response);
            }

            Entered?.Set();
            Release?.Wait();

            switch (Mode)
            {
                case ServerHandlerMode.Respond:
                {
                    response.Response(Payload(value + 1));
                    break;
                }
                case ServerHandlerMode.RespondAndFinish:
                {
                    response.Response(Payload(value + 1));
                    response.Finish();
                    break;
                }
                case ServerHandlerMode.Reject:
                {
                    response.Reject();
                    break;
                }
                case ServerHandlerMode.Throw:
                {
                    throw new InvalidOperationException("server handler failure");
                }
                default:
                    break;
            }
        }
        finally
        {
            request.Release();
            Requested.Set();
            Interlocked.Decrement(ref _activeCount);
        }
    }

    private static UnionDataList Payload(int value)
    {
        UnionDataList payload = MemoryRental.Shared.CollectablePool.Acquire<UnionDataList>();
        payload.PutLast(new UnionData(value));
        return payload;
    }

    private void UpdateMax(int active)
    {
        int max = Volatile.Read(ref _maxActiveCount);

        while (active > max)
        {
            int previous = Interlocked.CompareExchange(ref _maxActiveCount, active, max);

            if (previous == max)
            {
                return;
            }

            max = previous;
        }
    }
}
