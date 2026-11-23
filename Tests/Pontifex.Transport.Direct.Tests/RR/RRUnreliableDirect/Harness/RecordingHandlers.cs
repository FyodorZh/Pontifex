using System;
using System.Threading;
using Actuarius.Memory;
using Pontifex.RR;
using Pontifex.RR.Unreliable;
using Pontifex.Utils;

namespace Pontifex.Tests.RR.RRUnreliableDirect.Harness;

internal sealed class RecordingClientHandler : RRRequestHandler, IRRUnreliableRequestHandler,
    IDisposable
{
    private int _responseCount;

    public ManualResetEventSlim Responded { get; } = new(false);

    public int ResponseCount => Volatile.Read(ref _responseCount);

    public int LastResponseValue { get; private set; }

    public int LastResponseSize { get; private set; }

    public void Response(UnionDataList response)
    {
        try
        {
            Interlocked.Increment(ref _responseCount);
            LastResponseSize = response.GetDataSize();

            if (response.Elements.Count > 0)
            {
                LastResponseValue = response.Elements[0].Alias.IntValue;
            }

            Responded.Set();
        }
        finally
        {
            response.Release();
        }
    }

    public void Dispose()
    {
        Responded.Dispose();
    }
}

internal enum ServerResponseMode
{
    Respond,
    AcceptSilently,
    Reject
}

internal sealed class RecordingServerHandler : IRRUnreliableServerHandler, IDisposable
{
    private readonly ServerResponseMode _mode;
    private readonly IMemoryRental _memory;
    private readonly object _valuesLock = new();
    private readonly List<int> _requestValues = new();
    private readonly List<SendResult> _responseResults = new();
    private int _requestCount;
    private int _activeCount;
    private int _maxActiveCount;

    public RecordingServerHandler(
        IMemoryRental memory,
        ServerResponseMode mode = ServerResponseMode.Respond)
    {
        _memory = memory;
        _mode = mode;
    }

    public ManualResetEventSlim Requested { get; } = new(false);

    public ManualResetEventSlim? Entered { get; set; }

    public ManualResetEventSlim? Release { get; set; }

    public int RequestCount => Volatile.Read(ref _requestCount);

    public int MaxActiveCount => Volatile.Read(ref _maxActiveCount);

    public bool? ObservedStartReturned { get; private set; }

    public Func<bool>? StartReturnedProbe { get; set; }

    public int LastRequestValue { get; private set; }

    public int[] RequestValues
    {
        get
        {
            lock (_valuesLock)
            {
                return _requestValues.ToArray();
            }
        }
    }

    public SendResult[] ResponseResults
    {
        get
        {
            lock (_valuesLock)
            {
                return _responseResults.ToArray();
            }
        }
    }

    public bool OnRequested(IEndPoint client, UnionDataList request, IRRUnreliableResponse response)
    {
        int active = Interlocked.Increment(ref _activeCount);
        Interlocked.Increment(ref _requestCount);
        UpdateMax(active);

        try
        {
            Entered?.Set();
            Release?.Wait();
            ObservedStartReturned = StartReturnedProbe?.Invoke();

            if (request.Elements.Count > 0)
            {
                LastRequestValue = request.Elements[0].Alias.IntValue;
                lock (_valuesLock)
                {
                    _requestValues.Add(LastRequestValue);
                }
            }

            switch (_mode)
            {
                case ServerResponseMode.Reject:
                    return false;
                case ServerResponseMode.AcceptSilently:
                    return true;
                default:
                    var payload = _memory.CollectablePool.Acquire<UnionDataList>();
                    payload.PutLast(LastRequestValue + 1);
                    SendResult result = response.Response(payload);
                    lock (_valuesLock)
                    {
                        _responseResults.Add(result);
                    }

                    return true;
            }
        }
        finally
        {
            request.Release();
            Interlocked.Decrement(ref _activeCount);
            Requested.Set();
        }
    }

    public void Dispose()
    {
        Requested.Dispose();
        Entered?.Dispose();
        Release?.Dispose();
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
