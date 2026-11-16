using System;
using Actuarius.Memory;
using Pontifex.Raw.Unreliable;
using Pontifex.Utils;

namespace Pontifex.Tests.Endpoints;

/// <summary>
/// Recording unreliable handler. Inherits the received/stopped recording and buffer cloning from
/// <see cref="RecordingRawHandler"/> and adds the start notification. It is shared by the unreliable
/// endpoint layers.
/// </summary>
internal class RecordingRawUnreliableHandler : RecordingRawHandler, IRawUnreliableHandler
{
    private bool _startCompleted;

    public RecordingRawUnreliableHandler(IMemoryRental memory)
        : base(memory)
    {
    }

    /// <summary>
    /// Gets or sets a value indicating whether <see cref="OnStarted"/> throws after recording the call,
    /// modelling a handler that violates its contract.
    /// </summary>
    public bool ThrowOnStarted { get; set; }

    public int StartedCount { get; private set; }

    public IRawUnreliableEndpoint? StartedEndpoint { get; private set; }

    /// <summary>
    /// Gets the number of delivered buffers observed after <see cref="OnStarted"/> completed successfully.
    /// </summary>
    public int ReceivedAfterStartedCount { get; private set; }

    /// <summary>
    /// Gets the number of delivered buffers observed before <see cref="OnStarted"/> completed. A non-zero
    /// value means a message was delivered without a preceding completed start notification.
    /// </summary>
    public int ReceivedBeforeStartedCount { get; private set; }

    /// <summary>
    /// Gets or sets a probe invoked inside <see cref="OnStarted"/>, before the start completion flag is set.
    /// A test uses it to observe the endpoint's pending scheduled actions at that moment.
    /// </summary>
    public Action? OnStartedProbe { get; set; }

    public void OnStarted(IRawUnreliableEndpoint endpoint)
    {
        StartedCount++;
        StartedEndpoint = endpoint;
        RecordEvent("started");
        OnStartedProbe?.Invoke();

        if (ThrowOnStarted)
        {
            throw new InvalidOperationException("OnStarted failed");
        }

        _startCompleted = true;
    }

    public override void OnReceived(UnionDataList receivedBuffer)
    {
        if (_startCompleted)
        {
            ReceivedAfterStartedCount++;
        }
        else
        {
            ReceivedBeforeStartedCount++;
        }

        base.OnReceived(receivedBuffer);
    }
}
