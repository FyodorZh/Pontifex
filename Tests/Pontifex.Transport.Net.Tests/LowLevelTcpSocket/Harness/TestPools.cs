using Actuarius.Memory;

namespace Pontifex.Tests.NetSockets.LowLevelTcpSocketTests.Harness
{
    /// <summary>Helpers for building pools used by the test suite.</summary>
    internal static class TestPools
    {
        /// <summary>
        /// Wraps the shared real byte-array pool in a <see cref="ByteArrayConcurrentPoolGuard"/> so tests
        /// can assert leak-free / double-release-free ownership via <c>ErrorCount</c>/<c>AssertNoOutstanding</c>.
        /// </summary>
        public static ByteArrayConcurrentPoolGuard GuardedRealPool()
            => new ByteArrayConcurrentPoolGuard(MemoryRental.Shared.ByteArraysPool);
    }
}
