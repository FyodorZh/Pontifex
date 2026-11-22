using System;
using Actuarius.Memory;
using Pontifex.Raw.Unreliable;
using Scriba;

namespace Pontifex.Factory.Tests.Bootstrap.Harness
{
    // Minimal raw-unreliable client used only to exercise the built-in
    // RawUnreliable -> RawReliable converter through the default graph.
    // It performs no I/O and never starts a thread.
    internal sealed class FakeRawUnreliableClient : IRawUnreliableClient
    {
        public TransportType Type => TransportType.RawUnreliable;

        public string Name => "fake-unreliable";

        public bool IsValid => true;

        public bool IsStarted => false;

        public ILogger Log => VoidLogger.Instance;

        public IMemoryRental Memory => MemoryRental.Shared;

        public int MessageMaxByteSize => 1024;

        public bool Start(Action<StopReason> onStopped)
        {
            if (onStopped is null)
            {
                throw new ArgumentNullException(nameof(onStopped));
            }

            return false;
        }

        public bool Stop(StopReason? reason = null)
        {
            return true;
        }

        public void GetControls(System.Collections.Generic.List<IControl> dst, Predicate<IControl>? predicate = null)
        {
        }

        public bool Init(IRawUnreliableHandler handler)
        {
            if (handler is null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            return true;
        }
    }
}
