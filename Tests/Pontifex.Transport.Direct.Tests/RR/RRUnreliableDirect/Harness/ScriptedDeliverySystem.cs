using System;
using System.Collections.Generic;
using Actuarius.Memory;
using Pontifex.Utils;
using Pontifex.VirtualDelivery;

namespace Pontifex.Tests.RR.RRUnreliableDirect.Harness;

internal enum DeliveryAction
{
    Deliver,
    Drop,
    Duplicate,
    Throw
}

/// <summary>
/// Test-only delivery system that consumes a deterministic script of deliver, drop, duplicate, and
/// throw actions.
/// </summary>
internal sealed class ScriptedDeliverySystem : IDeliverySystem
{
    private readonly Queue<DeliveryAction> _script = new();
    private DeliveryAction _default = DeliveryAction.Deliver;

    public int DeliverCount { get; private set; }

    public int DropCount { get; private set; }

    public int DuplicateCount { get; private set; }

    public int ThrowCount { get; private set; }

    public event Action<UnionDataList>? Delivered;

    public ScriptedDeliverySystem Then(params DeliveryAction[] actions)
    {
        foreach (DeliveryAction action in actions)
        {
            _script.Enqueue(action);
        }

        return this;
    }

    public ScriptedDeliverySystem WithDefault(DeliveryAction action)
    {
        _default = action;
        return this;
    }

    public void Deliver(UnionDataList message)
    {
        DeliveryAction action = _script.Count > 0 ? _script.Dequeue() : _default;

        switch (action)
        {
            case DeliveryAction.Drop:
                DropCount++;
                message.Release();
                return;
            case DeliveryAction.Throw:
                ThrowCount++;
                throw new InvalidOperationException("scripted delivery failure");
            case DeliveryAction.Duplicate:
                DuplicateCount++;
                var clone = message.Clone(MemoryRental.Shared.CollectablePool);
                DeliverToSubscribers(message);
                DeliverToSubscribers(clone);
                return;
            default:
                DeliverCount++;
                DeliverToSubscribers(message);
                return;
        }
    }

    public void Clear()
    {
    }

    private void DeliverToSubscribers(UnionDataList message)
    {
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
}
