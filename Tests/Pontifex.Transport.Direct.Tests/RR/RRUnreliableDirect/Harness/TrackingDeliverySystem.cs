using System;
using System.Threading;
using Pontifex.Utils;
using Pontifex.VirtualDelivery;

namespace Pontifex.Tests.RR.RRUnreliableDirect.Harness;

/// <summary>
/// Test-only delivery system that records delivery, clear, and dispose calls and can be scripted to
/// fail.
/// </summary>
internal sealed class TrackingDeliverySystem : IDeliverySystem, IDisposable
{
    private readonly bool _throwOnDeliver;
    private int _deliverCount;
    private int _clearCount;
    private int _disposeCount;

    public TrackingDeliverySystem(bool throwOnDeliver = false)
    {
        _throwOnDeliver = throwOnDeliver;
    }

    public int DeliverCount => Volatile.Read(ref _deliverCount);

    public int ClearCount => Volatile.Read(ref _clearCount);

    public int DisposeCount => Volatile.Read(ref _disposeCount);

    public bool IsReleased => ClearCount > 0 || DisposeCount > 0;

    public event Action<UnionDataList>? Delivered;

    public void Deliver(UnionDataList message)
    {
        Interlocked.Increment(ref _deliverCount);

        if (_throwOnDeliver)
        {
            throw new InvalidOperationException("scripted delivery failure");
        }

        Action<UnionDataList>? delivered = Delivered;
        if (delivered != null)
        {
            delivered(message);
        }
        else
        {
            message.Release();
        }
    }

    public void Clear()
    {
        Interlocked.Increment(ref _clearCount);
    }

    public void Dispose()
    {
        Interlocked.Increment(ref _disposeCount);
    }
}
