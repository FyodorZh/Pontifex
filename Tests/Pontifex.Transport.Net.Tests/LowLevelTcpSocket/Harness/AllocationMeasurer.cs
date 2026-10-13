using System;

namespace Pontifex.Tests.NetSockets.LowLevelTcpSocketTests.Harness
{
    /// <summary>
    /// Calibrated managed-allocation measurement for the transport test fixtures. Uses the
    /// per-thread allocated-bytes counter around a synchronous window, with explicit warmup and a
    /// thread-migration check: a measured window must run on one managed thread, because the counter
    /// is thread-local. The thread-id seam exists so calibration tests can prove migration is
    /// rejected without actually migrating a thread.
    /// </summary>
    internal static class AllocationMeasurer
    {
        /// <summary>
        /// Runs <paramref name="measured"/> <paramref name="warmupIterations"/> times (JIT/pool
        /// warmup outside the window), then returns the managed bytes allocated by the current thread
        /// across <paramref name="measureIterations"/> measured invocations. Throws when the thread
        /// changed during the window.
        /// </summary>
        public static long Measure(
            Action measured,
            int warmupIterations = 64,
            int measureIterations = 1024,
            Func<int>? threadIdProvider = null)
        {
            if (measured == null) throw new ArgumentNullException(nameof(measured));
            if (warmupIterations < 0) throw new ArgumentOutOfRangeException(nameof(warmupIterations));
            if (measureIterations <= 0) throw new ArgumentOutOfRangeException(nameof(measureIterations));

            threadIdProvider ??= CurrentThreadId;

            for (int i = 0; i < warmupIterations; i++)
            {
                measured();
            }

            int threadId = threadIdProvider();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < measureIterations; i++)
            {
                measured();
            }

            long after = GC.GetAllocatedBytesForCurrentThread();

            if (threadIdProvider() != threadId)
            {
                throw new InvalidOperationException(
                    "The measured window must not migrate across managed threads: the allocation " +
                    "counter is thread-local and the result would be meaningless.");
            }

            return after - before;
        }

        /// <summary>Measured bytes per invocation, rounded up to the nearest whole byte.</summary>
        public static double MeasurePerOperation(
            Action measured,
            int warmupIterations = 64,
            int measureIterations = 1024,
            Func<int>? threadIdProvider = null)
            => (double)Measure(measured, warmupIterations, measureIterations, threadIdProvider) / measureIterations;

        private static int CurrentThreadId() => Environment.CurrentManagedThreadId;
    }
}
