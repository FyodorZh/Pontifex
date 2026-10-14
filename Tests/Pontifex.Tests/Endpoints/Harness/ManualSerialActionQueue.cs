using System;
using System.Collections.Generic;
using Pontifex.Utils;

namespace Pontifex.Tests.Endpoints;

/// <summary>
/// A single-threaded, manually drained <see cref="ISerialActionQueue"/> used to make endpoint
/// tests deterministic. It models the semantics of the production serial queue:
/// actions run strictly one at a time, an action that schedules another has it run on a later
/// drain iteration (never inline), and an exception from an action is routed to the error callback
/// and the drain continues.
/// </summary>
/// <remarks>
/// Endpoints never call <c>Release</c> or <c>ScheduleFast</c>, so neither is modelled: every
/// accepted action is eventually run. <c>onFailToRun</c> is therefore never invoked.
/// </remarks>
internal sealed class ManualSerialActionQueue : ISerialActionQueue
{
    private readonly Queue<Action> _actions = new();
    private readonly Action<Exception> _onError;

    public ManualSerialActionQueue(Action<Exception> onError)
    {
        _onError = onError;
    }

    /// <summary>
    /// Gets the number of actions accepted but not yet run.
    /// </summary>
    public int PendingCount => _actions.Count;

    public bool Schedule(Action action)
    {
        _actions.Enqueue(action);
        return true;
    }

    public bool Schedule<T>(T param, Action<T> action, Action<T>? onFailToRun) where T : class
    {
        _actions.Enqueue(() => action(param));
        return true;
    }

    /// <summary>
    /// Runs the next accepted action, if any. Exceptions are routed to the error callback.
    /// </summary>
    public void DrainOne()
    {
        if (_actions.Count == 0)
        {
            return;
        }

        Action action = _actions.Dequeue();
        try
        {
            action();
        }
        catch (Exception e)
        {
            _onError(e);
        }
    }

    /// <summary>
    /// Runs accepted actions until none remain, including actions scheduled during the drain.
    /// </summary>
    public void DrainAll()
    {
        while (_actions.Count > 0)
        {
            DrainOne();
        }
    }
}
