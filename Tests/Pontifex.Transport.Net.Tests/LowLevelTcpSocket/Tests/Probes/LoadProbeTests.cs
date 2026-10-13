using System.Diagnostics;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using Actuarius.Memory;
using Pontifex.Tests.NetSockets.LowLevelTcpSocketTests.Harness;

namespace Pontifex.Tests.NetSockets.LowLevelTcpSocketTests
{
    /// <summary>
    /// Load probes (`Load` category, Release only). They report throughput, latency
    /// percentiles (p50/p95/p99), GC counts, and retained storage for representative compositions:
    /// small-message steady state, competing connections, shutdown storms, and idle-after-large-send.
    /// Sizes are bounded so the probes stay in the low seconds; a unit assertion is never a
    /// substitute for these comparisons.
    /// </summary>
    [LowLevelTcpSocketSuite]
    public class LoadProbeTests
    {
        [Test]
        [Category(TestCategories.Load)]
        public void SteadyStateSmallMessage_ThroughputAndLatency()
        {
            using var harness = new LowLevelSocketHarness();
            InstallSynchronousSends(harness);
            var buffer = new MultiRefByteArray(new byte[64]);
            const int operations = 20_000;
            var latencies = new long[operations];

            void Operation()
            {
                buffer.AddRef();
                harness.EnqueueSend(buffer);
                harness.Pump();
            }

            for (int i = 0; i < 256; i++)
            {
                Operation();
            }

            GC.Collect();
            int gen0 = GC.CollectionCount(0);
            int gen1 = GC.CollectionCount(1);
            int gen2 = GC.CollectionCount(2);
            var watch = Stopwatch.StartNew();
            for (int i = 0; i < operations; i++)
            {
                long start = Stopwatch.GetTimestamp();
                Operation();
                latencies[i] = Stopwatch.GetTimestamp() - start;
            }

            watch.Stop();

            ProbeReport(
                "one connection, 64-byte messages",
                operations,
                watch,
                latencies,
                GC.CollectionCount(0) - gen0,
                GC.CollectionCount(1) - gen1,
                GC.CollectionCount(2) - gen2);

            Assert.That(harness.Queue.PopCount, Is.GreaterThanOrEqualTo(256 + operations));
            Assert.That(harness.Accessor.SendCount, Is.EqualTo(256 + operations),
                "each 64-byte message must complete synchronously in the pump loop");
        }

        [Test]
        [Category(TestCategories.Load)]
        public void CompetingConnections_ThroughputAndLatency()
        {
            const int connectionCount = 4;
            const int operationsPerConnection = 5_000;
            var harnesses = new LowLevelSocketHarness[connectionCount];
            var buffers = new MultiRefByteArray[connectionCount];
            try
            {
                for (int i = 0; i < connectionCount; i++)
                {
                    harnesses[i] = new LowLevelSocketHarness();
                    InstallSynchronousSends(harnesses[i]);
                    buffers[i] = new MultiRefByteArray(new byte[64]);
                }

                void Operation(int connection)
                {
                    IMultiRefByteArray buffer = buffers[connection];
                    buffer.AddRef();
                    harnesses[connection].EnqueueSend(buffer);
                    harnesses[connection].Pump();
                }

                for (int i = 0; i < 256; i++)
                {
                    Operation(i % connectionCount);
                }

                int total = connectionCount * operationsPerConnection;
                var latencies = new long[total];
                GC.Collect();
                int gen0 = GC.CollectionCount(0);
                int gen1 = GC.CollectionCount(1);
                int gen2 = GC.CollectionCount(2);
                var watch = Stopwatch.StartNew();
                for (int i = 0; i < total; i++)
                {
                    long start = Stopwatch.GetTimestamp();
                    Operation(i % connectionCount);
                    latencies[i] = Stopwatch.GetTimestamp() - start;
                }

                watch.Stop();

                ProbeReport(
                    $"{connectionCount} competing connections, 64-byte messages",
                    total,
                    watch,
                    latencies,
                    GC.CollectionCount(0) - gen0,
                    GC.CollectionCount(1) - gen1,
                    GC.CollectionCount(2) - gen2);
            }
            finally
            {
                foreach (LowLevelSocketHarness harness in harnesses)
                {
                    harness?.Dispose();
                }
            }
        }

        [Test]
        [Category(TestCategories.Load)]
        public void ShutdownStorm_ConnectionChurn_AllocationAndTiming()
        {
            const int connections = 200;
            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            int gen0 = GC.CollectionCount(0);
            var watch = Stopwatch.StartNew();
            for (int i = 0; i < connections; i++)
            {
                using var harness = new LowLevelSocketHarness();
                harness.EnqueueSend(new MultiRefByteArray(Array.Empty<byte>()));
                harness.Pump();
            }

            watch.Stop();
            long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
            int collections = GC.CollectionCount(0) - gen0;

            TestContext.Out.WriteLine(
                $"[load-probe] shutdown storm: {connections} one-shot connections, " +
                $"{allocated} bytes total ({allocated / (double)connections:F0} bytes/connection), " +
                $"{watch.Elapsed.TotalMilliseconds / connections * 1000:F1} us/connection, " +
                $"{collections} gen0 collections.");

            Assert.That(allocated, Is.GreaterThan(0),
                "connection churn necessarily allocates; the probe records the baseline");
        }

        [Test]
        [Category(TestCategories.Load)]
        public void IdleAfterLargeSend_ReleasedPayloadsAreCollectible()
        {
            const int connections = 32;
            const int payloadSize = 256 * 1024;
            var harnesses = new List<LowLevelSocketHarness>(connections);
            var weakReferences = new List<WeakReference>(connections);
            long before = GC.GetTotalMemory(forceFullCollection: true);
            try
            {
                for (int i = 0; i < connections; i++)
                {
                    var harness = new LowLevelSocketHarness();
                    InstallSynchronousSends(harness);
                    harnesses.Add(harness);
                    weakReferences.Add(SendLargeAndDrain(harness, payloadSize));
                }

                GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
                GC.WaitForPendingFinalizers();
                GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);

                long after = GC.GetTotalMemory(forceFullCollection: true);
                int alive = weakReferences.Count(reference => reference.IsAlive);

                TestContext.Out.WriteLine(
                    $"[load-probe] idle after large send: {connections} connections x {payloadSize} bytes, " +
                    $"managed heap delta {after - before} bytes, {alive} payload(s) still reachable.");

                Assert.That(alive, Is.EqualTo(0),
                    "released idle payloads must be collectible while the connections stay alive");
            }
            finally
            {
                foreach (LowLevelSocketHarness harness in harnesses)
                {
                    harness.Dispose();
                }

                GC.KeepAlive(harnesses);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static WeakReference SendLargeAndDrain(LowLevelSocketHarness harness, int payloadSize)
        {
            var payload = new byte[payloadSize];
            harness.EnqueueSend(new MultiRefByteArray(payload));
            harness.Pump();
            return new WeakReference(payload);
        }

        private static void InstallSynchronousSends(LowLevelSocketHarness harness)
        {
            harness.Accessor.OnSend = static args =>
            {
                ((TestSocketAsyncEventArgs)args).SetSynchronousResult(args.Count, SocketError.Success);
                return false;
            };
        }

        private static void ProbeReport(
            string label,
            int operations,
            Stopwatch watch,
            long[] latencies,
            int gen0,
            int gen1,
            int gen2)
        {
            Array.Sort(latencies);
            double opsPerSecond = operations / watch.Elapsed.TotalSeconds;
            TestContext.Out.WriteLine(
                $"[load-probe] {label}: {operations} ops in {watch.Elapsed.TotalMilliseconds:F1} ms " +
                $"({opsPerSecond:F0} ops/s), p50 {PercentileMs(latencies, 0.50):F3} ms, " +
                $"p95 {PercentileMs(latencies, 0.95):F3} ms, p99 {PercentileMs(latencies, 0.99):F3} ms, " +
                $"max {PercentileMs(latencies, 1.0):F3} ms, GC gen0/gen1/gen2 {gen0}/{gen1}/{gen2}.");
        }

        private static double PercentileMs(long[] sortedTicks, double percentile)
        {
            int index = Math.Clamp((int)Math.Ceiling(percentile * sortedTicks.Length) - 1, 0, sortedTicks.Length - 1);
            return sortedTicks[index] * 1000.0 / Stopwatch.Frequency;
        }
    }
}
