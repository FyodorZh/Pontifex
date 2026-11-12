using System;
using System.Collections.Generic;
using Actuarius.Memory;
using Scriba;

namespace Pontifex.Factory.Tests.Compilation.Harness;

// Minimal live transport double. It exposes only the ITransport surface needed by
// the suite plus settable identity, so tests can recognise the exact instance the
// compiler returned. It never performs real I/O.
internal sealed class FakeTransport : ITransport
{
    public FakeTransport(
        TransportType type,
        string name = "fake",
        ILogger? log = null,
        IMemoryRental? memory = null)
    {
        Type = type;
        Name = name;
        Log = log ?? VoidLogger.Instance;
        Memory = memory ?? MemoryRental.Shared;
    }

    public TransportType Type { get; set; }

    public string Name { get; set; }

    public bool IsValid { get; set; } = true;

    public bool IsStarted { get; private set; }

    public ILogger Log { get; }

    public IMemoryRental Memory { get; }

    public bool Start(Action<StopReason> onStopped)
    {
        if (onStopped is null)
        {
            throw new ArgumentNullException(nameof(onStopped));
        }

        IsStarted = true;
        return true;
    }

    public bool Stop(StopReason? reason = null)
    {
        IsStarted = false;
        return true;
    }

    public void GetControls(List<IControl> dst, Predicate<IControl>? predicate = null)
    {
    }
}
