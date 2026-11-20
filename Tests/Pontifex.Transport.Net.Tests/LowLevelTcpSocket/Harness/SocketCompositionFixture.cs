using System;
using Actuarius.Memory;
using Pontifex.NetSockets.Tcp;

namespace Pontifex.Tests.NetSockets.LowLevelTcpSocketTests.Harness
{
    /// <summary>
    /// Test-local composition fixture whose teardown is part of the harness contract: it completes
    /// outstanding fake native operations (so borrowed buffers return through the late native
    /// completion path) and leaves the guarded pool balanced.
    /// </summary>
    internal sealed class SocketCompositionFixture : IDisposable
    {
        public SocketCompositionFixture(ByteArrayConcurrentPoolGuard pool)
        {
            Pool = pool;
            Harness = new LowLevelSocketHarness(pool: pool);
        }

        public LowLevelSocketHarness Harness { get; }

        public ByteArrayConcurrentPoolGuard Pool { get; }

        public void Dispose()
        {
            Harness.Socket.Dispose();
            Harness.CompletePendingOperations();
        }
    }
}
