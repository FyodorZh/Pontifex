using System;
using System.Collections.Generic;
using Actuarius.Memory;
using Scriba;

namespace Pontifex.Tests.Transports;

/// <summary>
/// Test double for <see cref="AnyTransport"/>. It implements the abstract lifecycle hooks, records
/// their invocations, and lets each test choose the result of <see cref="TryStart"/> or supply
/// behavior for the started/stopped hooks.
/// </summary>
internal sealed class TestAnyTransport : AnyTransport
{
    private readonly List<string> _events = new();

    public TestAnyTransport(
        string typeName = "TestTransport",
        ILogger? logger = null,
        IMemoryRental? memory = null)
        : base(typeName, logger ?? new Logger([]), memory ?? MemoryRental.Shared)
    {
    }

    public override TransportType Type => TransportType.RawReliableAck;

    /// <summary>
    /// Gets or sets the result returned from <see cref="TryStart"/>.
    /// </summary>
    public Func<bool> TryStartBehavior { get; set; } = static () => true;

    public Action? OnStartedBehavior { get; set; }

    public Action<StopReason>? OnStoppedBehavior { get; set; }

    public int TryStartCalls { get; private set; }

    public int OnStartedCalls { get; private set; }

    public int OnStoppedCalls { get; private set; }

    public StopReason? LastOnStoppedReason { get; private set; }

    /// <summary>
    /// Gets the value of <see cref="ITransport.IsStarted"/> observed from inside
    /// <see cref="OnStopped"/>.
    /// </summary>
    public bool? IsStartedDuringOnStopped { get; private set; }

    /// <summary>
    /// Gets the value of <see cref="ITransport.IsValid"/> observed from inside
    /// <see cref="OnStopped"/>.
    /// </summary>
    public bool? IsValidDuringOnStopped { get; private set; }

    /// <summary>
    /// Gets the hook names in invocation order (<c>tryStart</c>, <c>started</c>, <c>stopped</c>).
    /// </summary>
    public IReadOnlyList<string> Events => _events;

    public bool ExposedHasStartBeenAttempted => HasStartBeenAttempted;

    protected override bool TryStart()
    {
        TryStartCalls++;
        _events.Add("tryStart");
        return TryStartBehavior();
    }

    protected override void OnStarted()
    {
        OnStartedCalls++;
        _events.Add("started");
        OnStartedBehavior?.Invoke();
    }

    protected override void OnStopped(StopReason reason)
    {
        OnStoppedCalls++;
        LastOnStoppedReason = reason;
        IsStartedDuringOnStopped = IsStarted;
        IsValidDuringOnStopped = IsValid;
        _events.Add("stopped");
        OnStoppedBehavior?.Invoke(reason);
    }
}
