using System;
using System.Collections.Generic;
using Pontifex.Utils;

namespace Pontifex.Tests.Transports;

/// <summary>
/// A single-threaded <see cref="ISerialActionQueue"/> used to drive server-session endpoints
/// deterministically. It can either queue actions for an explicit drain or run them inline, and it can
/// reject or throw from the next scheduling call so the transport's admission failure paths can be
/// exercised without a real carrier or thread.
/// </summary>
internal sealed class ScriptedSerialActionQueue : ISerialActionQueue
{
    private readonly Queue<Action> _actions = new();
    private readonly Action<Exception> _onError;

    public ScriptedSerialActionQueue(Action<Exception> onError)
    {
        _onError = onError;
    }

    /// <summary>
    /// Gets or sets whether scheduled actions run inline instead of being queued.
    /// </summary>
    public bool RunImmediately { get; set; }

    /// <summary>
    /// Gets or sets whether the next <c>Schedule</c> call is rejected with <see langword="false"/>.
    /// The flag is cleared after it is observed.
    /// </summary>
    public bool RejectNextSchedule { get; set; }

    /// <summary>
    /// Gets or sets an exception thrown from the next <c>Schedule</c> call. The value is cleared after
    /// it is observed.
    /// </summary>
    public Exception? ThrowNextSchedule { get; set; }

    public bool Schedule(Action action)
    {
        if (TryConsumeScheduleFailure())
        {
            return false;
        }

        Run(action);
        return true;
    }

    public bool Schedule<T>(T param, Action<T> action, Action<T>? onFailToRun) where T : class
    {
        if (TryConsumeScheduleFailure())
        {
            return false;
        }

        Run(() => action(param));
        return true;
    }

    /// <summary>
    /// Runs the next accepted action, if any. Exceptions are routed to the error callback.
    /// </summary>
    private void DrainOne()
    {
        if (_actions.Count == 0)
        {
            return;
        }

        Action action = _actions.Dequeue();
        Execute(action);
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

    private bool TryConsumeScheduleFailure()
    {
        if (ThrowNextSchedule != null)
        {
            Exception exception = ThrowNextSchedule;
            ThrowNextSchedule = null;
            throw exception;
        }

        if (RejectNextSchedule)
        {
            RejectNextSchedule = false;
            return true;
        }

        return false;
    }

    private void Run(Action action)
    {
        if (RunImmediately)
        {
            Execute(action);
        }
        else
        {
            _actions.Enqueue(action);
        }
    }

    private void Execute(Action action)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            _onError(exception);
        }
    }
}
