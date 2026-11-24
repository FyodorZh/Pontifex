using System;
using System.Threading;
using Pontifex.Utils;
using Pontifex.VirtualDelivery;

namespace Pontifex.Tests.Raw.TestSupport;

/// <summary>
/// Delivery system double that stalls the delivery worker at <see cref="Deliver"/> until
/// <see cref="Release"/> is called. It lets a test fill an endpoint's commit queue deterministically instead
/// of relying on a scheduling race.
/// </summary>
public sealed class BlockingDeliverySystem : IDeliverySystem
{
    private readonly ManualResetEventSlim _gate = new(false);
    private int _deliverCalls;
    private int _completedCalls;

    /// <summary>
    /// Gets the number of messages that entered <see cref="Deliver"/>.
    /// </summary>
    public int DeliverCalls => Volatile.Read(ref _deliverCalls);

    /// <summary>
    /// Gets the number of messages whose delivery completed.
    /// </summary>
    public int CompletedCalls => Volatile.Read(ref _completedCalls);

    /// <summary>
    /// Gets a signal that completes when the first message enters <see cref="Deliver"/>.
    /// </summary>
    public TaskCompletionSource FirstDelivery { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// Gets a signal that completes when the requested number of deliveries has completed.
    /// </summary>
    public TaskCompletionSource ExpectedCompletions { get; private set; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private int _expectedCompletions = 1;

    /// <inheritdoc/>
    public event Action<UnionDataList>? Delivered;

    /// <summary>
    /// Sets the number of completed deliveries that completes <see cref="ExpectedCompletions"/>.
    /// </summary>
    /// <param name="count">The expected completion count; must be positive.</param>
    public void ExpectCompletions(int count)
    {
        if (count <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        _expectedCompletions = count;
        ExpectedCompletions = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    /// <inheritdoc/>
    public void Deliver(UnionDataList message)
    {
        Interlocked.Increment(ref _deliverCalls);
        FirstDelivery.TrySetResult();
        _gate.Wait();

        try
        {
            Delivered?.Invoke(message);
        }
        finally
        {
            if (Interlocked.Increment(ref _completedCalls) >= Volatile.Read(ref _expectedCompletions))
            {
                ExpectedCompletions.TrySetResult();
            }
        }
    }

    /// <summary>
    /// Releases every blocked delivery.
    /// </summary>
    public void Release()
    {
        _gate.Set();
    }

    /// <inheritdoc/>
    public void Clear()
    {
    }
}
