using System.Diagnostics;
using System.Net.Sockets;
using Actuarius.Memory;
using Pontifex.NetSockets.Tcp;
using Pontifex.Tests.NetSockets.LowLevelTcpSocketTests.Harness;

namespace Pontifex.Tests.NetSockets.LowLevelTcpSocketTests
{
    /// <summary>
    /// Profiling matrix (Release-only <c>Load</c> probes). Every probe is bounded and
    /// prints warmed measurements; assertions are sanity/balance checks only, never timing budgets.
    /// Observer overhead is kept separate (preallocated buffers, no-copy consumers, fake-completion
    /// overhead reported explicitly where relevant).
    /// </summary>
    [LowLevelTcpSocketSuite]
    public class TransportProfilingProbes
    {
        [Test]
        [Category(TestCategories.Load)]
        public void ColdConnectionChurn_VsLongLived_AllocationPerConnection()
        {
            const int connections = 200;
            long argsCreated = 0;
            long argsDisposed = 0;
            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            int gen0 = GC.CollectionCount(0);
            var watch = Stopwatch.StartNew();
            for (int i = 0; i < connections; i++)
            {
                var harness = new LowLevelSocketHarness();
                harness.EnqueueSend(new MultiRefByteArray(Array.Empty<byte>()));
                harness.Pump();
                argsCreated += harness.Accessor.CreateEventArgsCount;
                harness.Dispose();
                argsDisposed += harness.Accessor.DisposeEventArgsCount;
            }

            watch.Stop();
            long churnBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
            int churnGen0 = GC.CollectionCount(0) - gen0;

            using var longLived = new LowLevelSocketHarness();
            longLived.Accessor.OnSend = static args =>
            {
                ((TestSocketAsyncEventArgs)args).SetSynchronousResult(args.Count, SocketError.Success);
                return false;
            };
            var buffer = new MultiRefByteArray(new byte[64]);
            void Send()
            {
                buffer.AddRef();
                longLived.EnqueueSend(buffer);
                longLived.Pump();
            }

            for (int i = 0; i < 256; i++)
            {
                Send();
            }

            long steadyBefore = GC.GetAllocatedBytesForCurrentThread();
            var steadyWatch = Stopwatch.StartNew();
            for (int i = 0; i < 20_000; i++)
            {
                Send();
            }

            steadyWatch.Stop();
            long steadyBytes = GC.GetAllocatedBytesForCurrentThread() - steadyBefore;

            TestContext.Out.WriteLine(
                $"[stage24] cold churn: {connections} connections, {churnBytes} B total " +
                $"({churnBytes / (double)connections:F0} B/connection), " +
                $"{watch.Elapsed.TotalMilliseconds / connections * 1000:F1} us/connection, " +
                $"args created/disposed {argsCreated}/{argsDisposed}, {churnGen0} gen0; " +
                $"long-lived: 20000 sends, {steadyBytes} B ({steadyBytes / 20000.0:F2} B/op), " +
                $"{20_000 / steadyWatch.Elapsed.TotalSeconds:F0} ops/s.");

            Assert.That(argsCreated, Is.EqualTo(2 * connections),
                "each connection creates two directional args");
            Assert.That(argsDisposed, Is.EqualTo(2 * connections),
                "each connection disposes both args exactly once");
            Assert.That(steadyBytes, Is.LessThan(20_000 * 64),
                "long-lived steady state is not dominated by per-op metadata");
        }

        [Test]
        [Category(TestCategories.Load)]
        public void AmbientContext_SyncTransport_AndRuntimeCaptureCost()
        {
            long? syncBaseline = null;
            foreach ((string label, string? value, int slots) in AmbientScenarios())
            {
                using var ambient = AmbientScope.Enter(value, slots);

                using var harness = new LowLevelSocketHarness();
                harness.Accessor.OnSend = static args =>
                {
                    ((TestSocketAsyncEventArgs)args).SetSynchronousResult(args.Count, SocketError.Success);
                    return false;
                };
                var buffer = new MultiRefByteArray(new byte[64]);
                void Send()
                {
                    buffer.AddRef();
                    harness.EnqueueSend(buffer);
                    harness.Pump();
                }

                for (int i = 0; i < 256; i++)
                {
                    Send();
                }

                long syncBytes = AllocationMeasurer.Measure(Send, 0, 10_000);
                const int captureIterations = 100_000;
                long captureBytes = AllocationMeasurer.Measure(
                    () =>
                    {
                        ExecutionContext? captured = ExecutionContext.Capture();
                        if (captured != null)
                        {
                            ExecutionContext.Run(captured, static _ => { }, null);
                            captured.Dispose();
                        }
                    },
                    64, captureIterations);
                var watch = Stopwatch.StartNew();
                for (int i = 0; i < captureIterations; i++)
                {
                    ExecutionContext? captured = ExecutionContext.Capture();
                    if (captured != null)
                    {
                        ExecutionContext.Run(captured, static _ => { }, null);
                        captured.Dispose();
                    }
                }

                watch.Stop();
                double captureNs = watch.Elapsed.TotalMilliseconds * 1_000_000 / captureIterations;

                TestContext.Out.WriteLine(
                    $"[stage24] ambient '{label}' ({slots} slot(s)): sync transport 10000 ops " +
                    $"{syncBytes} B, runtime capture+run {captureBytes} B/{captureIterations} ops " +
                    $"({captureNs:F1} ns/op).");

                if (syncBaseline == null)
                {
                    syncBaseline = syncBytes;
                }
                else
                {
                    Assert.That(syncBytes, Is.EqualTo(syncBaseline.Value),
                        "synchronous transport allocation must not depend on ambient context size");
                }
            }
        }

        [Test]
        [Category(TestCategories.Load)]
        public async Task AmbientContext_RealSocketAsync_ByContextSize()
        {
            foreach ((string label, string? value, int slots) in AmbientScenarios())
            {
                using var ambient = AmbientScope.Enter(value, slots);
                const int warmup = 64;
                const int operations = 128;
                using var fixture = RealSocketFixture.Create();
                var buffer = new MultiRefByteArray(new byte[64]);
                Socket peer = fixture.Peer;
                int totalBytes = (warmup + operations) * 64;
                Task<int> drain = Task.Run(async () =>
                {
                    var sink = new byte[totalBytes + 4096];
                    int got = 0;
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                    while (got < totalBytes)
                    {
                        int n = await peer.ReceiveAsync(
                            sink.AsMemory(got, Math.Min(sink.Length - got, 4096)),
                            SocketFlags.None, cts.Token);
                        if (n == 0)
                        {
                            break;
                        }

                        got += n;
                    }

                    return got;
                });

                void Send()
                {
                    buffer.AddRef();
                    fixture.Source.Put(buffer);
                    fixture.Transport.Sender.Pump();
                }

                for (int i = 0; i < warmup; i++)
                {
                    Send();
                }

                int postsBefore = fixture.Accessor.SendPosts;
                int pendingBefore = fixture.Accessor.PendingSendObservations;
                long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
                var watch = Stopwatch.StartNew();
                for (int i = 0; i < operations; i++)
                {
                    Send();
                }

                watch.Stop();
                long delta = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
                int posts = fixture.Accessor.SendPosts - postsBefore;
                int pending = fixture.Accessor.PendingSendObservations - pendingBefore;

                fixture.Transport.Sender.Shutdown(true);
                await TestWait.WaitCompletionAsync(fixture.Transport.Sender.Completion);
                await TestWait.WaitCompletionAsync(fixture.Transport.Sender.SourceQuiesced);
                int received = await TestWait.GetResultAsync(drain);

                TestContext.Out.WriteLine(
                    $"[stage24] ambient '{label}' real async: {operations} sends, {delta} B " +
                    $"({delta / (double)operations:F1} B/op), {watch.Elapsed.TotalMilliseconds * 1e6 / operations:F0} ns/op, " +
                    $"pending {pending}/{posts} posts, peer received {received} B.");

                Assert.That(received, Is.EqualTo(totalBytes),
                    "all sends must reach the peer for a meaningful measurement");
            }
        }

        [Test]
        [Category(TestCategories.Load)]
        public void ManyPendingReceives_StorageBudgetAndPoolReturns()
        {
            // Documented storage budget: 256 connections x 16 KiB receive window = 4 MiB of raw
            // receive storage (plus per-connection metadata).
            const int connections = 256;
            const int receiveSize = 16 * 1024;
            var harnesses = new List<LowLevelSocketHarness>(connections);
            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            try
            {
                for (int i = 0; i < connections; i++)
                {
                    var harness = new LowLevelSocketHarness(pool: ReceivePools.Shared, receiveBufferSize: receiveSize);
                    harness.StartReceiver();
                    harnesses.Add(harness);
                }

                long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
                long pending = harnesses.Sum(h => (long)h.Accessor.PendingCount);

                TestContext.Out.WriteLine(
                    $"[stage24] many pending receives: {connections} connections x {receiveSize} B " +
                    $"= {connections * receiveSize / (1024.0 * 1024.0):F1} MiB raw window, " +
                    $"{allocated} B connection metadata ({allocated / (double)connections:F0} B/connection), " +
                    $"{pending} native-pending operations.");

                Assert.That(pending, Is.EqualTo(connections),
                    "every connection must have exactly one native-pending receive");
            }
            finally
            {
                foreach (LowLevelSocketHarness harness in harnesses)
                {
                    harness.Socket.Dispose();
                    harness.CompletePendingOperations();
                }
            }

            long warm = AllocationMeasurer.Measure(
                () =>
                {
                    IMultiRefByteArray rented = ReceivePools.Shared.Acquire(receiveSize);
                    rented.Release();
                },
                64, 1024);
            TestContext.Out.WriteLine($"[stage24] pool after {connections} returns: warm acquire {warm} B/1024 ops.");
            Assert.That(warm, Is.LessThan(64),
                "recycled wrappers return to the shared pool after the pending-receive storm");
        }

        [Test]
        [Category(TestCategories.Load)]
        public void DelayedConsumerRetention_CachePressure_AndMisses()
        {
            IConcurrentPool<IMultiRefByteArray, int> pool = ReceivePools.Bounded(4);
            const int held = 16;
            IMultiRefByteArray[] leases = new IMultiRefByteArray[held];

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < held; i++)
            {
                leases[i] = pool.Acquire(16 * 1024);
            }

            long missBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            var ledger = new ApplicationByteLedger(held * 16L * 1024);
            foreach (IMultiRefByteArray lease in leases)
            {
                Assert.That(ledger.TryAdmitQueued(lease), Is.True);
            }

            foreach (IMultiRefByteArray lease in leases)
            {
                ledger.Remove(lease);
                lease.Release();
            }

            long warm = AllocationMeasurer.Measure(
                () =>
                {
                    IMultiRefByteArray buffer = pool.Acquire(16 * 1024);
                    buffer.Release();
                },
                64, 1024);

            TestContext.Out.WriteLine(
                $"[stage24] delayed retention: {held} simultaneous leases over a 4-entry cache, " +
                $"{missBytes} B for {held} acquisitions ({missBytes / (double)held:F0} B/acquisition on misses), " +
                $"warm acquire after release {warm} B/1024 ops.");

            Assert.That(missBytes, Is.GreaterThan(0),
                "a cache miss allocates a wrapper; the plateau is bounded by the cache capacity");
            Assert.That(warm, Is.LessThan(64),
                "returned wrappers refill the bounded cache");
        }

        [Test]
        [Category(TestCategories.Load)]
        public async Task ShutdownStorm_TimerEntriesAndCancellation()
        {
            const int connections = 64;
            var schedulers = new ManualDeadlineScheduler[connections];
            var executors = new ManualTeardownExecutor[connections];
            var harnesses = new List<LowLevelSocketHarness>(connections);
            var disposals = new List<Task>(connections);
            try
            {
                for (int i = 0; i < connections; i++)
                {
                    schedulers[i] = new ManualDeadlineScheduler();
                    executors[i] = new ManualTeardownExecutor();
                    var harness = new LowLevelSocketHarness(
                        teardownExecutor: executors[i],
                        disposalScheduler: schedulers[i]);
                    harnesses.Add(harness);
                    disposals.Add(harness.Socket.DisposeAsync(TimeSpan.FromSeconds(30)).AsTask());
                }

                int armed = schedulers.Count(s => s.HasPendingCallback);
                TestContext.Out.WriteLine(
                    $"[stage24] shutdown storm: {connections} bounded disposals, {armed} armed shared timers.");

                Assert.That(armed, Is.EqualTo(connections),
                    "one shared per-connection disposal deadline per connection");

                for (int i = 0; i < connections; i++)
                {
                    executors[i].Run();
                }

                foreach (Task disposal in disposals)
                {
                    await TestWait.WaitCompletionAsync(disposal);
                }

                for (int i = 0; i < connections; i++)
                {
                    executors[i].Join();
                }

                int stillArmed = schedulers.Count(s => s.HasPendingCallback);
                int disposeCalls = harnesses.Sum(h => h.Accessor.DisposeCount);
                TestContext.Out.WriteLine(
                    $"[stage24] shutdown storm complete: {stillArmed} armed timers, " +
                    $"{disposeCalls} accessor dispositions, {disposals.Count} bounded waiters resolved.");

                Assert.That(stillArmed, Is.EqualTo(0),
                    "successful disposal cancels the shared timer for every connection");
                Assert.That(disposeCalls, Is.EqualTo(connections),
                    "exactly one native disposition per connection");
            }
            finally
            {
                foreach (ManualTeardownExecutor executor in executors)
                {
                    executor?.Join();
                }

                foreach (LowLevelSocketHarness harness in harnesses)
                {
                    harness.Dispose();
                    harness.CompletePendingOperations();
                }
            }
        }

        [Test]
        [Category(TestCategories.Load)]
        public void CompetingConnections_NoFairnessBudget_ContentionAndLatency()
        {
            const int connectionCount = 8;
            const int operationsPerConnection = 4_000;
            int total = connectionCount * operationsPerConnection;
            var harnesses = new LowLevelSocketHarness[connectionCount];
            var buffers = new MultiRefByteArray[connectionCount];
            try
            {
                for (int i = 0; i < connectionCount; i++)
                {
                    harnesses[i] = new LowLevelSocketHarness();
                    harnesses[i].Accessor.OnSend = static args =>
                    {
                        ((TestSocketAsyncEventArgs)args).SetSynchronousResult(args.Count, SocketError.Success);
                        return false;
                    };
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

                var latencies = new long[total];
                GC.Collect();
                int gen0 = GC.CollectionCount(0);
                int gen1 = GC.CollectionCount(1);
                int gen2 = GC.CollectionCount(2);
                long contentionBefore = Monitor.LockContentionCount;
                TimeSpan pauseBefore = GC.GetTotalPauseDuration();
                var watch = Stopwatch.StartNew();
                for (int i = 0; i < total; i++)
                {
                    long start = Stopwatch.GetTimestamp();
                    Operation(i % connectionCount);
                    latencies[i] = Stopwatch.GetTimestamp() - start;
                }

                watch.Stop();
                long contention = Monitor.LockContentionCount - contentionBefore;
                TimeSpan pause = GC.GetTotalPauseDuration() - pauseBefore;
                Array.Sort(latencies);

                TestContext.Out.WriteLine(
                    $"[stage24] {connectionCount} competing connections, no fairness budget: {total} ops in " +
                    $"{watch.Elapsed.TotalMilliseconds:F1} ms ({total / watch.Elapsed.TotalSeconds:F0} ops/s), " +
                    $"p50 {PercentileMs(latencies, 0.50):F4} ms, p95 {PercentileMs(latencies, 0.95):F4} ms, " +
                    $"p99 {PercentileMs(latencies, 0.99):F4} ms, max {PercentileMs(latencies, 1.0):F4} ms, " +
                    $"lock contentions {contention}, GC gen0/1/2 {GC.CollectionCount(0) - gen0}/" +
                    $"{GC.CollectionCount(1) - gen1}/{GC.CollectionCount(2) - gen2}, pause {pause.TotalMilliseconds:F2} ms.");

                Assert.That(total, Is.EqualTo(connectionCount * operationsPerConnection));
            }
            finally
            {
                foreach (LowLevelSocketHarness harness in harnesses)
                {
                    harness?.Dispose();
                }
            }
        }

        private static IEnumerable<(string Label, string? Value, int Slots)> AmbientScenarios()
        {
            yield return ("none", null, 0);
            yield return ("one value", "correlation-id-0001", 1);
            yield return ("substantial", "correlation-id-0001", 16);
        }

        private static double PercentileMs(long[] sortedTicks, double percentile)
        {
            int index = Math.Clamp((int)Math.Ceiling(percentile * sortedTicks.Length) - 1, 0, sortedTicks.Length - 1);
            return sortedTicks[index] * 1000.0 / Stopwatch.Frequency;
        }

        private sealed class AmbientScope : IDisposable
        {
            private readonly List<AsyncLocal<string?>> _slots;

            private AmbientScope(List<AsyncLocal<string?>> slots)
            {
                _slots = slots;
            }

            public static AmbientScope Enter(string? value, int slots)
            {
                var holders = new List<AsyncLocal<string?>>(slots);
                for (int i = 0; i < slots; i++)
                {
                    var slot = new AsyncLocal<string?>();
                    slot.Value = i == 0 ? value : $"{value}-{i:D2}";
                    holders.Add(slot);
                }

                return new AmbientScope(holders);
            }

            public void Dispose()
            {
                foreach (AsyncLocal<string?> slot in _slots)
                {
                    slot.Value = null;
                }

                GC.KeepAlive(_slots);
            }
        }
    }
}
