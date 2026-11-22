using System;
using System.Threading;
using Pontifex.Utils;
using Pontifex.VirtualDelivery;

namespace Pontifex.Tests.Channels.Harness;

/// <summary>
/// Test-only delivery system that records clear and dispose calls.
/// </summary>
internal sealed class TrackingDeliverySystem : IDeliverySystem, IDisposable
{
    private int _clearCount;
    private int _disposeCount;

    public int ClearCount => Volatile.Read(ref _clearCount);

    public int DisposeCount => Volatile.Read(ref _disposeCount);

    public event Action<UnionDataList>? Delivered;

    public void Deliver(UnionDataList message)
    {
        var delivered = Delivered;
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
