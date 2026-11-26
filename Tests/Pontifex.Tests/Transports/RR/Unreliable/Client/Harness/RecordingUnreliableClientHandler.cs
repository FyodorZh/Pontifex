using System;
using System.Collections.Generic;
using System.Threading;
using Pontifex.RR;
using Pontifex.RR.Unreliable;
using Pontifex.Utils;

namespace Pontifex.Tests.Transports;

/// <summary>
/// Client request handler that records every response and can act from inside a callback.
/// </summary>
internal sealed class RecordingUnreliableClientHandler : RRRequestHandler, IRRUnreliableRequestHandler
{
    private readonly object _locker = new object();
    private readonly List<int> _messageIds = new List<int>();
    private readonly List<int> _messageCounts = new List<int>();
    private readonly List<int> _responseValues = new List<int>();
    private int _activeCallbacks;
    private int _maxActiveCallbacks;

    public ManualResetEventSlim Entered { get; } = new ManualResetEventSlim(false);

    public ManualResetEventSlim Release { get; } = new ManualResetEventSlim(false);

    public bool BlockFirstCallback { get; set; }

    public bool ThrowOnResponse { get; set; }

    public bool CancelFromCallback { get; set; }

    public Action? OnResponse { get; set; }

    public bool BoundAfterCancel { get; private set; }

    public int ActiveCallbacks => Volatile.Read(ref _activeCallbacks);

    public int MaxActiveCallbacks => Volatile.Read(ref _maxActiveCallbacks);

    public int ResponseCount
    {
        get
        {
            lock (_locker)
            {
                return _messageIds.Count;
            }
        }
    }

    public int[] MessageIds
    {
        get
        {
            lock (_locker)
            {
                return _messageIds.ToArray();
            }
        }
    }

    public int[] MessageCounts
    {
        get
        {
            lock (_locker)
            {
                return _messageCounts.ToArray();
            }
        }
    }

    public int[] ResponseValues
    {
        get
        {
            lock (_locker)
            {
                return _responseValues.ToArray();
            }
        }
    }

    public void Response(UnionDataList response, int messageId, int messageCount)
    {
        int active = Interlocked.Increment(ref _activeCallbacks);
        UpdateMax(active);

        try
        {
            int value = response.Elements.Count > 0
                ? response.Elements[0].Alias.IntValue
                : int.MinValue;

            lock (_locker)
            {
                _messageIds.Add(messageId);
                _messageCounts.Add(messageCount);
                _responseValues.Add(value);
            }

            Entered.Set();

            OnResponse?.Invoke();

            if (CancelFromCallback)
            {
                Cancel();
                BoundAfterCancel = IsBound;
            }

            if (BlockFirstCallback && messageId == 0)
            {
                Release.Wait(TimeSpan.FromSeconds(5));
            }

            if (ThrowOnResponse)
            {
                throw new InvalidOperationException("client response handler failure");
            }
        }
        finally
        {
            response.Release();
            Interlocked.Decrement(ref _activeCallbacks);
        }
    }

    private void UpdateMax(int active)
    {
        int max = Volatile.Read(ref _maxActiveCallbacks);
        while (active > max)
        {
            int previous = Interlocked.CompareExchange(ref _maxActiveCallbacks, active, max);
            if (previous == max)
            {
                return;
            }

            max = previous;
        }
    }
}
