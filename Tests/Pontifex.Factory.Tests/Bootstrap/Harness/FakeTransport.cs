using System;
using System.Collections.Generic;
using Actuarius.Memory;
using Scriba;

namespace Pontifex.Factory.Tests.Bootstrap.Harness
{
    // Minimal live transport double. It exposes only the ITransport surface the
    // suite inspects, so tests can recognise the exact instance a constructor
    // returned. It never performs real I/O. The Compilation suite owns its own
    // copy, so this suite stays self-contained.
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

        public TransportType Type { get; }

        public string Name { get; }

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
}
