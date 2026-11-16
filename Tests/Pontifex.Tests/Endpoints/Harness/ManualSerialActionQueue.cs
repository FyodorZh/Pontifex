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
/// accepted action is eventually run. <c>onFailToRun</c> is therefore never invoked. Rejection is
/// available on demand through <see cref="RejectNextSchedule"/>.
/// </remarks>
internal sealed class ManualSerialActionQueue : ISerialActionQueue
{
    private readonly Queue<Action> _actions = new();
    private readonly Action<Exception> _onError;

    private bool _rejectNextSchedule;

    public ManualSerialActionQueue(Action<Exception> onError)
    {
        _onError = onError;
    }

    /// <summary>
    /// Rejects the next <see cref="Schedule(Action)"/> or <see cref="Schedule{T}"/> call, modelling a
    /// bounded or released queue. The flag is consumed by that call and is off by default.
    /// </summary>
    /// <remarks>
    /// A rejected action is dropped exactly like the production queue drops it: without enqueueing it
    /// and without invoking the <c>onFailToRun</c> callback.
    /// </remarks>
    public void RejectNextSchedule()
    {
        _rejectNextSchedule = true;
    }

    public bool Schedule(Action action)
    {
        if (_rejectNextSchedule)
        {
            _rejectNextSchedule = false;
            return false;
        }

        _actions.Enqueue(action);
        return true;
    }

    public bool Schedule<T>(T param, Action<T> action, Action<T>? onFailToRun) where T : class
    {
        if (_rejectNextSchedule)
        {
            _rejectNextSchedule = false;
            return false;
        }

        _actions.Enqueue(() => action(param));
        return true;
    }

    /// <summary>
    /// Gets the number of accepted actions that have not run yet.
    /// </summary>
    /// <remarks>
    /// Exposed so a test can observe where a callback ran relative to the actions the endpoint had already
    /// scheduled, without inspecting production state.
    /// </remarks>
    public int PendingCount => _actions.Count;

    /// <summary>
    /// Runs exactly one accepted action, the oldest, so a test can interleave scheduling and
    /// execution. Does nothing when the queue is empty.
    /// </summary>
    /// <remarks>
    /// An action may schedule further actions; they are not run by this call. An exception from the
    /// action is routed to the error callback, as in <see cref="DrainAll"/>.
    /// </remarks>
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
