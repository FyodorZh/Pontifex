using System;
using System.Collections.Generic;
using System.Threading;
using Actuarius.Memory;
using Pontifex.RR;
using Pontifex.RR.Reliable;
using Pontifex.Utils;

namespace Pontifex.Tests.RR.RRReliableDirect.Harness;

/// <summary>
/// Client request handler that records every callback, releases every payload, and can act from
/// inside a callback.
/// </summary>
internal sealed class RecordingReliableClientHandler : RRRequestHandler, IRRReliableRequestHandler,
    IDisposable
{
    private readonly object _locker = new();
    private readonly List<int> _responses = new();
    private readonly List<RRReliableCompletionType> _finishes = new();

    public ManualResetEventSlim Responded { get; } = new(false);

    public ManualResetEventSlim Finished { get; } = new(false);

    public Action<UnionDataList>? OnResponse { get; set; }

    public Action<RRReliableCompletionType>? OnFinish { get; set; }

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

    public int LastResponseSize { get; private set; }

    public void Response(UnionDataList response)
    {
        try
        {
            int value = response.Elements.Count > 0 ? response.Elements[0].Alias.IntValue : int.MinValue;
            LastResponseSize = response.GetDataSize();

            lock (_locker)
            {
                _responses.Add(value);
            }

            Responded.Set();
            OnResponse?.Invoke(response);
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
    }

    public void ResetSignals()
    {
        Responded.Reset();
        Finished.Reset();
    }

    public void Dispose()
    {
        Responded.Dispose();
        Finished.Dispose();
    }
}

internal enum ReliableServerMode
{
    AcceptSilently,
    Respond,
    RespondAndFinish,
    Reject,
    TwoResponsesThenFinish
}

/// <summary>
/// Server handler that records every presented request and can answer, reject, or leave the handle
/// undecided for the test.
/// </summary>
internal sealed class RecordingReliableServerHandler : IRRReliableServerHandler, IDisposable
{
    private readonly IMemoryRental _memory;
    private readonly object _locker = new();
    private readonly List<int> _requestValues = new();
    private readonly List<IEndPoint> _requestSources = new();
    private readonly List<IRRReliableResponse> _handles = new();
    private readonly List<SendResult> _responseResults = new();
    private int _activeCount;
    private int _maxActiveCount;

    public RecordingReliableServerHandler(
        IMemoryRental memory,
        ReliableServerMode mode = ReliableServerMode.AcceptSilently)
    {
        _memory = memory;
        Mode = mode;
    }

    public ReliableServerMode Mode { get; set; }

    public ManualResetEventSlim Requested { get; } = new(false);

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

    public int LastRequestSize { get; private set; }

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

    public long[] RequestIds
    {
        get
        {
            lock (_locker)
            {
                var ids = new long[_handles.Count];
                for (int i = 0; i < _handles.Count; i++)
                {
                    ids[i] = _handles[i].RequestId;
                }

                return ids;
            }
        }
    }

    public SendResult[] ResponseResults
    {
        get
        {
            lock (_locker)
            {
                return _responseResults.ToArray();
            }
        }
    }

    public IRRReliableResponse? HandleForValue(int value)
    {
        lock (_locker)
        {
            for (int i = 0; i < _requestValues.Count; i++)
            {
                if (_requestValues[i] == value)
                {
                    return _handles[i];
                }
            }

            return null;
        }
    }

    public void OnRequested(IEndPoint client, UnionDataList request, IRRReliableResponse response)
    {
        int active = Interlocked.Increment(ref _activeCount);
        UpdateMax(active);

        try
        {
            int value = request.Elements.Count > 0 ? request.Elements[0].Alias.IntValue : int.MinValue;
            LastRequestSize = request.GetDataSize();

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
                case ReliableServerMode.Respond:
                    Record(response.Response(Payload(value + 1)));
                    break;
                case ReliableServerMode.RespondAndFinish:
                    Record(response.Response(Payload(value + 1)));
                    Record(response.Finish());
                    break;
                case ReliableServerMode.Reject:
                    Record(response.Reject());
                    break;
                case ReliableServerMode.TwoResponsesThenFinish:
                    Record(response.Response(Payload(value * 10 + 1)));
                    Record(response.Response(Payload(value * 10 + 2)));
                    Record(response.Finish());
                    break;
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

    private void Record(SendResult result)
    {
        lock (_locker)
        {
            _responseResults.Add(result);
        }
    }

    private UnionDataList Payload(int value)
    {
        UnionDataList payload = _memory.CollectablePool.Acquire<UnionDataList>();
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

    public void Dispose()
    {
        Requested.Dispose();
        Entered?.Dispose();
        Release?.Dispose();
    }
}
