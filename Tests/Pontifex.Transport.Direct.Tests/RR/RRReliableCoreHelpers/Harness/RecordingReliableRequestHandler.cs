using System.Collections.Generic;
using Pontifex.RR;
using Pontifex.RR.Reliable;
using Pontifex.Utils;

namespace Pontifex.Tests.RR.RRReliableCoreHelpers.Harness;

/// <summary>
/// Client request handler that records the callbacks it receives and releases every payload.
/// </summary>
internal sealed class RecordingReliableRequestHandler : RRRequestHandler, IRRReliableRequestHandler
{
    private readonly object _locker = new();
    private readonly List<int> _responses = new();
    private readonly List<RRReliableCompletionType> _finishes = new();

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
            int value = response.Elements.Count > 0 ? response.Elements[0].Alias.IntValue : -1;

            lock (_locker)
            {
                _responses.Add(value);
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
    }
}
