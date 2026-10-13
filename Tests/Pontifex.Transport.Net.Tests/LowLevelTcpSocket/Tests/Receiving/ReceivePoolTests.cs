using System.Net.Sockets;
using Actuarius.Memory;
using Pontifex.NetSockets.Tcp;
using Pontifex.Tests.NetSockets.LowLevelTcpSocketTests.Harness;

namespace Pontifex.Tests.NetSockets.LowLevelTcpSocketTests
{
    /// <summary>
    /// Adopt the dependency-provided allocation-free receive pool (P02 rental-wrapper
    /// portion). The default non-recycling pool remains the allocating baseline and arbitrary
    /// injected pools keep their behavior; <see cref="ReceivePools"/> is the documented opt-in
    /// composition entry point. Red evidence: the opt-in path still allocated a wrapper per
    /// acquisition before the factory used the dependency's recyclable pools.
    /// </summary>
    [LowLevelTcpSocketSuite]
    public class ReceivePoolTests
    {
        [Test]
        [Category(TestCategories.Allocation)]
        public void OptInPool_WarmedAcquireRelease_HasNoOwnerMetadataAllocation()
        {
            const int size = 16 * 1024;
            const int warmup = 64;
            const int iterations = 1024;

            long legacyDelta = AllocationMeasurer.Measure(
                () =>
                {
                    IMultiRefByteArray buffer = ReceivePools.NonRecycling.Acquire(size);
                    buffer.Release();
                },
                warmup, iterations);
            long sharedDelta = AllocationMeasurer.Measure(
                () =>
                {
                    IMultiRefByteArray buffer = ReceivePools.Shared.Acquire(size);
                    buffer.Release();
                },
                warmup, iterations);
            IConcurrentPool<IMultiRefByteArray, int> bounded = ReceivePools.Bounded(8);
            long boundedDelta = AllocationMeasurer.Measure(
                () =>
                {
                    IMultiRefByteArray buffer = bounded.Acquire(size);
                    buffer.Release();
                },
                warmup, iterations);

            TestContext.Out.WriteLine(
                $"[stage21] warmed acquire/release at {size}: legacy {legacyDelta} B, " +
                $"shared {sharedDelta} B, bounded(8) {boundedDelta} B over {iterations} ops.");

            Assert.That(legacyDelta, Is.GreaterThan(0),
                "the non-recycling pool remains the allocating baseline by design");
            Assert.That(sharedDelta, Is.LessThan(64),
                $"SharedByteArraysPool warm acquire/release must allocate no owner metadata (measured {sharedDelta} B)");
            Assert.That(boundedDelta, Is.LessThan(64),
                $"a bounded recycling pool must allocate no owner metadata when warm (measured {boundedDelta} B)");

            // Documented Count semantics: Count is the requested capacity; the raw array is rounded
            // up to the next power of two and may be larger.
            IMultiRefByteArray rented = ReceivePools.Shared.Acquire(16015);
            Assert.That(rented.Count, Is.EqualTo(16015),
                "IMultiRefByteArray.Count from the opt-in pool is the requested capacity");
            Assert.That(rented.Array, Is.Not.Null);
            Assert.That(rented.Array!.Length, Is.EqualTo(16384),
                "the raw backing array is rounded up to the next power of two");
            rented.Release();
        }

        [Test]
        [Category(TestCategories.Allocation)]
        public void FullAndPartialTransportReceives_UseZeroSteadyStateMetadataAllocation()
        {
            const int warmup = 64;
            const int iterations = 1024;

            using var optIn = new TransportReceiveFixture(ReceivePools.Shared, 16 * 1024);
            long optInFull = AllocationMeasurer.Measure(optIn.DeliverFull, warmup, iterations);
            long optInPartial = AllocationMeasurer.Measure(optIn.DeliverPartial, warmup, iterations);

            using var legacy = new TransportReceiveFixture(ReceivePools.NonRecycling, 16 * 1024);
            long legacyFull = AllocationMeasurer.Measure(legacy.DeliverFull, warmup, iterations);

            TestContext.Out.WriteLine(
                $"[stage21] transport receives over {iterations}: opt-in full {optInFull} B, " +
                $"opt-in partial {optInPartial} B, legacy full {legacyFull} B.");

            Assert.That(optIn.Consumer.Deliveries, Is.EqualTo(2 * (warmup + iterations)));
            Assert.That(legacy.Consumer.Deliveries, Is.EqualTo(warmup + iterations));
            Assert.That(optInFull, Is.LessThan(legacyFull - (48L * iterations)),
                "owner-wrapper recycling must remove the per-receive rental allocation");
            Assert.That(optInPartial - optInFull, Is.LessThan(64),
                $"the partial view plus recycled owner must add no per-receive allocation " +
                $"(full {optInFull}, partial {optInPartial})");
        }

        [Test]
        public void OwnerRetainedByConsumer_CannotBeReused()
        {
            using var harness = new LowLevelSocketHarness(pool: ReceivePools.Shared);
            var delivered = new List<IMultiRefByteArray>();
            harness.Sink.ReleaseBuffers = false;
            harness.Sink.OnReceived = buffer => delivered.Add(buffer);

            harness.StartReceiver();
            for (int i = 0; i < 9; i++)
            {
                harness.CompleteReceive(16 * 1024); // full buffers: the consumer receives owners
            }

            Assert.That(delivered, Has.Count.EqualTo(9));
            Assert.That(delivered.Distinct(ReferenceEqualityComparer.Instance).Count(), Is.EqualTo(9),
                "a wrapper with a live consumer reference must never be handed out again");
            foreach (IMultiRefByteArray owner in delivered)
            {
                Assert.That(owner.IsAlive, Is.True);
                Assert.That(owner.Count, Is.EqualTo(16 * 1024));
                Assert.That(owner.Array, Is.Not.Null);
            }

            // Consumer-owned storage survives connection disposal; the wrappers stay live.
            harness.Socket.Dispose();
            harness.CompletePendingOperations();
            foreach (IMultiRefByteArray owner in delivered)
            {
                Assert.That(owner.IsAlive, Is.True,
                    "consumer-owned storage must survive transport disposal");
            }

            foreach (IMultiRefByteArray owner in delivered)
            {
                owner.Release();
            }

            // After every exact-once release the pool remains usable and hands out live storage.
            IMultiRefByteArray reused = ReceivePools.Shared.Acquire(16 * 1024);
            Assert.That(reused.IsAlive, Is.True);
            reused[0] = 42;
            Assert.That(reused[0], Is.EqualTo(42));
            reused.Release();
        }

        [Test]
        public void PoolMissAndCacheLimit_AreSafe()
        {
            IConcurrentPool<IMultiRefByteArray, int> bounded = ReceivePools.Bounded(2);
            var held = new List<IMultiRefByteArray>();
            for (int i = 0; i < 4; i++)
            {
                held.Add(bounded.Acquire(4096));
            }

            Assert.That(held.Distinct(ReferenceEqualityComparer.Instance).Count(), Is.EqualTo(4),
                "a pool miss must never alias a live wrapper");
            foreach (IMultiRefByteArray buffer in held)
            {
                Assert.That(buffer.IsAlive, Is.True);
                Assert.That(buffer.Count, Is.EqualTo(4096));
            }

            foreach (IMultiRefByteArray buffer in held)
            {
                buffer.Release();
            }

            long warmDelta = AllocationMeasurer.Measure(
                () =>
                {
                    IMultiRefByteArray buffer = bounded.Acquire(4096);
                    buffer.Release();
                },
                64, 1024);
            Assert.That(warmDelta, Is.LessThan(64),
                $"a bounded recycling pool is allocation-free when warm (measured {warmDelta} B)");

            long legacyDelta = AllocationMeasurer.Measure(
                () =>
                {
                    IMultiRefByteArray buffer = ReceivePools.NonRecycling.Acquire(4096);
                    buffer.Release();
                },
                64, 256);
            Assert.That(legacyDelta, Is.GreaterThan(0),
                "the old non-recycling pool still allocates a wrapper per acquisition (documented)");
        }

        [Test]
        public void RecyclingPool_RepeatedReceives_ExactlyOnceRelease_NoAliasing()
        {
            using var fixture = new TransportReceiveFixture(ReceivePools.Bounded(4), 16 * 1024);
            byte expected = 0;
            fixture.Consumer.Validate = buffer =>
            {
                if (buffer.Count == 0 || buffer[0] != expected)
                {
                    throw new InvalidOperationException(
                        $"recycled-owner corruption: expected 0x{expected:X2}, got " +
                        $"{(buffer.Count == 0 ? "empty" : $"0x{buffer[0]:X2}")}");
                }
            };

            const int iterations = 256;
            for (int i = 0; i < iterations; i++)
            {
                expected = (byte)(i & 0xFF);
                if ((i & 1) == 0)
                {
                    fixture.DeliverFull(expected);
                }
                else
                {
                    fixture.DeliverPartial(expected);
                }

                Assert.That(fixture.Consumer.Deliveries, Is.EqualTo(i + 1));
            }

            Assert.That(fixture.Consumer.ReleaseCount, Is.EqualTo(iterations));

            // The pool is still coherent after the mixed soak.
            IMultiRefByteArray buffer = ReceivePools.Bounded(4).Acquire(16 * 1024);
            Assert.That(buffer.IsAlive, Is.True);
            buffer.Release();
        }

        [Test]
        public async Task LateNativeCompletion_ReturnsRecycledOwnerExactlyOnce()
        {
            var executor = new ManualTeardownExecutor();
            IConcurrentPool<IMultiRefByteArray, int> pool = ReceivePools.Bounded(4);
            using var harness = new LowLevelSocketHarness(pool: pool, teardownExecutor: executor);
            harness.StartReceiver();
            Assert.That(harness.Accessor.PendingCount, Is.EqualTo(1));

            harness.Socket.ForceAbortFinalize(new TimeoutException("stage21 late completion"));
            executor.Run();
            harness.CompleteReceive(0, SocketError.OperationAborted);

            await TestWait.WaitCompletionAsync(harness.Receiver.Completion);

            // The late exactly-once release returned the owner to the recyclable pool; the next
            // acquisition must be live with an intact capacity.
            IMultiRefByteArray reused = pool.Acquire(16 * 1024);
            Assert.That(reused.IsAlive, Is.True);
            Assert.That(reused.Count, Is.EqualTo(16 * 1024));
            reused[0] = 7;
            Assert.That(reused[0], Is.EqualTo(7));
            reused.Release();
        }

        [Test]
        public void ConcurrentFinalRelease_RecyclingPool_IsSafe()
        {
            for (int i = 0; i < 128; i++)
            {
                IMultiRefByteArray buffer = ReceivePools.Shared.Acquire(1024);
                buffer.AddRef();

                var worker = new BackgroundAction(() => buffer.Release(), "stage21-concurrent-release");
                buffer.Release();
                worker.Join();

                IMultiRefByteArray next = ReceivePools.Shared.Acquire(1024);
                Assert.That(next.IsAlive, Is.True);
                next.Release();
            }
        }

        [Test]
        public async Task AcquireFailure_FaultsReceiver_AndCleanupBalances()
        {
            var pool = new ThrowingPool(ReceivePools.Bounded(4))
            {
                AcquireException = new InvalidOperationException("pool acquire failure"),
            };
            using var harness = new LowLevelSocketHarness(pool: pool);

            harness.StartReceiver();

            ReceiverCompletionResult result = await TestWait.GetResultAsync(harness.Receiver.Completion);
            Assert.That(result.Reason, Is.EqualTo(ReceiverCompletionReason.Faulted));
            Assert.That(result.Error, Is.InstanceOf<InvalidOperationException>());
        }

        [Test]
        public async Task ThrowingBufferReturn_IsCapturedAndCompletionStillPublishes()
        {
            // The effective "pool return" of a refcounted owner is its own final Release; model a
            // failing return with a one-shot throwing buffer and drive it through terminal cleanup.
            var pool = new ThrowingReturnPool();
            using var harness = new LowLevelSocketHarness(pool: pool);
            harness.StartReceiver();

            harness.Receiver.Shutdown();
            harness.CompleteReceive(0, SocketError.Success); // terminal cleanup releases the in-flight owner
            harness.Socket.Dispose(); // closes the send direction so the aggregate resolves

            SocketCompletionResult result = await TestWait.GetResultAsync(harness.Socket.Completion);
            Assert.That(result.Reason, Is.EqualTo(SocketCompletionReason.Faulted),
                "a cleanup failure with no reserved initiating fault becomes the primary reason");
            Assert.That(result.Error, Is.InstanceOf<InvalidOperationException>(),
                "a failing buffer/pool return must be captured as a cleanup error, not swallowed");
        }

        [Test]
        public void GuardedDefaultPool_StillDiagnosesBalance()
        {
            var guard = new ByteArrayConcurrentPoolGuard(MemoryRental.Shared.ByteArraysPool, true, true);
            IMultiRefByteArray buffer = guard.Acquire(4096);
            buffer.Release();
            guard.AssertNoOutstanding();
            Assert.That(guard.ErrorCount, Is.EqualTo(0),
                "guarded diagnostics over the default pool remain balanced");
        }

        private sealed class ThrowingPool : IConcurrentPool<IMultiRefByteArray, int>
        {
            private readonly IConcurrentPool<IMultiRefByteArray, int> _inner;

            public ThrowingPool(IConcurrentPool<IMultiRefByteArray, int> inner)
            {
                _inner = inner;
            }

            public Exception? AcquireException { get; set; }

            public Exception? ReleaseException { get; set; }

            public IMultiRefByteArray Acquire(int param0)
            {
                if (AcquireException != null)
                {
                    throw AcquireException;
                }

                return _inner.Acquire(param0);
            }

            public (IMultiRefByteArray resource, IPoolSink<IMultiRefByteArray> poolSink) AcquireEx(int param0)
                => (Acquire(param0), this);

            public void Release(IMultiRefByteArray? resource)
            {
                if (ReleaseException != null)
                {
                    throw ReleaseException;
                }

                _inner.Release(resource);
            }
        }

        private sealed class ThrowingReturnPool : IConcurrentPool<IMultiRefByteArray, int>
        {
            public IMultiRefByteArray Acquire(int param0)
                => new ThrowingReleaseByteArray(new byte[param0]);

            public (IMultiRefByteArray resource, IPoolSink<IMultiRefByteArray> poolSink) AcquireEx(int param0)
                => (Acquire(param0), this);

            public void Release(IMultiRefByteArray? resource)
            {
            }
        }

        private sealed class TransportReceiveFixture : IDisposable
        {
            private readonly byte[] _fullPayload;
            private readonly byte[] _partialPayload = { 1, 2, 3 };

            public TransportReceiveFixture(
                IConcurrentPool<IMultiRefByteArray, int> pool,
                int bufferSize)
            {
                BufferSize = bufferSize;
                _fullPayload = new byte[bufferSize];
                Socket = new LowLevelTcpSocket(
                    Accessor,
                    Source,
                    Consumer.Handle,
                    pool,
                    RemoteHalfClosePolicy.KeepSendOpen,
                    null,
                    bufferSize);
                Socket.Receiver.Start();
            }

            public FakeSocketAccessor Accessor { get; } = new();

            public ControllableQueue Source { get; } = new();

            public NoCopyReceiveConsumer Consumer { get; } = new();

            public LowLevelTcpSocket Socket { get; }

            public int BufferSize { get; }

            public void DeliverFull() => DeliverFull((byte)0x5A);

            public void DeliverFull(byte fill)
            {
                Array.Fill(_fullPayload, fill);
                Deliver(_fullPayload);
            }

            public void DeliverPartial() => DeliverPartial(1);

            public void DeliverPartial(byte fill)
            {
                _partialPayload[0] = fill;
                Deliver(_partialPayload);
            }

            public void Dispose()
            {
                Socket.Dispose();
                Accessor.CompletePendingOperations();
            }

            private void Deliver(byte[] payload)
            {
                TestSocketAsyncEventArgs args = Accessor.LastReceiveArgs
                                               ?? throw new InvalidOperationException("no receive is pending");
                Buffer.BlockCopy(payload, 0, args.Buffer!, args.Offset, payload.Length);
                args.Complete(payload.Length, SocketError.Success);
            }
        }
    }
}
