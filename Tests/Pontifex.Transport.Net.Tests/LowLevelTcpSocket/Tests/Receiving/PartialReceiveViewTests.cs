using System.Net.Sockets;
using Actuarius.Memory;
using Pontifex.NetSockets.Tcp;
using Pontifex.Tests.NetSockets.LowLevelTcpSocketTests.Harness;

namespace Pontifex.Tests.NetSockets.LowLevelTcpSocketTests
{
    /// <summary>
    /// Reuse partial-receive views safely (P02 span allocation). Only the allocation case
    /// is necessarily red; the lifetime tests are guards against unsafe optimization. A view is
    /// bound to exactly one source range, holds exactly one source reference while live, is returned
    /// to a bounded pool only after the final consumer release, and is never mutated while any
    /// consumer reference remains.
    /// </summary>
    [LowLevelTcpSocketSuite]
    public class PartialReceiveViewTests
    {
        [Test]
        [Category(TestCategories.Allocation)]
        public void ReleasedPartialViews_ReusedAtSteadyState_NoPerReceiveViewAllocation()
        {
            using var fixture = new PartialReceiveAllocationFixture(16 * 1024);
            const int warmup = 64;
            const int iterations = 1024;

            // Calibrate the full-buffer path (which never creates a view) to subtract the fake
            // completion's own overhead from the partial path differentially.
            long fullDelta = AllocationMeasurer.Measure(fixture.DeliverFull, warmup, iterations);
            long partialDelta = AllocationMeasurer.Measure(fixture.DeliverPartial, warmup, iterations);

            TestContext.Out.WriteLine(
                $"[stage20] full-path delta {fullDelta} bytes, partial-path delta {partialDelta} bytes " +
                $"over {iterations} deliveries (consumer deliveries {fixture.Consumer.Deliveries}).");

            Assert.That(fixture.Consumer.Deliveries, Is.EqualTo(2 * (warmup + iterations)));
            Assert.That(fixture.Consumer.DeliveredBytes,
                Is.EqualTo((long)(warmup + iterations) * ((16 * 1024) + 3)));
            Assert.That(partialDelta - fullDelta, Is.LessThan(64),
                $"pooled partial views must not allocate per receive beyond the calibrated full path " +
                $"(partial {partialDelta}, full {fullDelta})");
        }

        [Test]
        public void RetainedViews_AreIndependentAcrossReceives()
        {
            using var harness = new LowLevelSocketHarness();
            var rented = new List<TrackingByteArray>();
            harness.Controllable!.Factory = _ =>
            {
                var buffer = new TrackingByteArray(new byte[100]);
                rented.Add(buffer);
                return buffer;
            };

            harness.Sink.ReleaseBuffers = false;
            IMultiRefByteArray? first = null;
            IMultiRefByteArray? second = null;
            harness.Sink.OnReceived = buffer =>
            {
                if (first == null)
                {
                    first = buffer;
                }
                else
                {
                    second ??= buffer;
                }

                buffer.AddRef();
            };

            harness.StartReceiver();
            harness.DeliverReceive(new byte[] { 1, 2, 3 });
            harness.DeliverReceive(new byte[] { 9, 8 });

            Assert.That(first, Is.Not.Null);
            Assert.That(second, Is.Not.Null);
            Assert.That(second, Is.Not.SameAs(first), "a second receive must use a different view");
            Assert.That(first!.IsAlive, Is.True, "the retained first view stays valid across later receives");

            Assert.That(first.Count, Is.EqualTo(3));
            Assert.That(first.Offset, Is.EqualTo(0));
            Assert.That(first[0], Is.EqualTo(1));
            Assert.That(first[1], Is.EqualTo(2));
            Assert.That(first[2], Is.EqualTo(3));

            Assert.That(second!.Count, Is.EqualTo(2));
            Assert.That(second[0], Is.EqualTo(9));
            Assert.That(second[1], Is.EqualTo(8));

            // Release the extra consumer references in different thread orders.
            var releaseFirstWorker = new BackgroundAction(() => first.Release(), "stage20-release-first");
            var releaseSecondWorker = new BackgroundAction(() => second.Release(), "stage20-release-second");
            first.Release();
            second.Release();
            releaseFirstWorker.Join();
            releaseSecondWorker.Join();

            Assert.That(first.IsAlive, Is.False);
            Assert.That(second.IsAlive, Is.False);
            Assert.That(rented[0].FinalReleaseCount, Is.EqualTo(1));
            Assert.That(rented[0].HasMisuse, Is.False);
            Assert.That(rented[1].FinalReleaseCount, Is.EqualTo(1));
            Assert.That(rented[1].HasMisuse, Is.False);
        }

        [Test]
        public void View_ReturnedOnlyAfterFinalRelease()
        {
            using var harness = new LowLevelSocketHarness();
            var rented = new List<TrackingByteArray>();
            harness.Controllable!.Factory = _ =>
            {
                var buffer = new TrackingByteArray(new byte[100]);
                rented.Add(buffer);
                return buffer;
            };

            harness.Sink.ReleaseBuffers = false;
            IMultiRefByteArray? first = null;
            IMultiRefByteArray? second = null;
            IMultiRefByteArray? third = null;
            harness.Sink.OnReceived = buffer =>
            {
                if (first == null)
                {
                    first = buffer;
                }
                else if (second == null)
                {
                    second = buffer;
                }
                else
                {
                    third = buffer;
                }
            };

            harness.StartReceiver();
            harness.DeliverReceive(new byte[] { 1, 2, 3 });
            first!.AddRef(); // extra consumer reference: the view must not be pooled yet

            harness.DeliverReceive(new byte[] { 4, 5, 6 });
            Assert.That(second, Is.Not.Null);
            Assert.That(second, Is.Not.SameAs(first),
                "no pool reuse while any valid consumer reference remains");

            first.Release();
            first.Release(); // final release

            Assert.That(first.IsAlive, Is.False);
            Assert.That(first.Count, Is.EqualTo(0), "the released view is reset");
            Assert.That(rented[0].FinalReleaseCount, Is.EqualTo(1),
                "final release returns the underlying owner exactly once");
            Assert.That(rented[0].HasMisuse, Is.False);

            harness.DeliverReceive(new byte[] { 7 });
            Assert.That(third, Is.SameAs(first),
                "the final release returns the view metadata to the pool for reuse");

            second!.Release();
            third!.Release();
            Assert.That(rented[1].FinalReleaseCount, Is.EqualTo(1));
            Assert.That(rented[2].FinalReleaseCount, Is.EqualTo(1));
        }

        [Test]
        public void CallbackThrowAndForceClose_PreserveConsumerOwnership()
        {
            var executor = new ManualTeardownExecutor();
            using var harness = new LowLevelSocketHarness(teardownExecutor: executor);
            TrackingByteArray? rented = null;
            harness.Controllable!.Factory = _ => rented = new TrackingByteArray(new byte[100]);

            IMultiRefByteArray? retained = null;
            harness.Sink.ReleaseBuffers = false;
            harness.Sink.OnReceived = buffer =>
            {
                retained = buffer;
                buffer.AddRef();
            };
            harness.Sink.ThrowOnReceive = new InvalidOperationException("consumer failure");

            harness.StartReceiver();
            harness.DeliverReceive(new byte[] { 1, 2, 3 });

            Assert.That(retained, Is.Not.Null);
            Assert.That(rented, Is.Not.Null);

            harness.Socket.ForceAbortFinalize(new TimeoutException("stage20 force close"));
            executor.Run();
            harness.CompleteReceive(0, SocketError.OperationAborted);

            Assert.That(retained!.IsAlive, Is.True,
                "a handed-off view must not be reclaimed by the transport, even after a callback throw");
            Assert.That(retained.Count, Is.EqualTo(3));
            Assert.That(retained[0], Is.EqualTo(1));
            Assert.That(retained[2], Is.EqualTo(3));
            Assert.That(rented!.FinalReleaseCount, Is.EqualTo(0),
                "the underlying owner is still referenced by the retained view");

            retained.Release();
            retained.Release();

            Assert.That(rented.FinalReleaseCount, Is.EqualTo(1),
                "the consumer's final release returns the underlying owner exactly once");
            Assert.That(rented.HasMisuse, Is.False);
        }

        [Test]
        public void FullBufferReceive_HandsTheOwnerDirectly()
        {
            using var harness = new LowLevelSocketHarness();
            var rented = new List<TrackingByteArray>();
            harness.Controllable!.Factory = _ =>
            {
                var buffer = new TrackingByteArray(new byte[64]);
                rented.Add(buffer);
                return buffer;
            };

            IMultiRefByteArray? delivered = null;
            harness.Sink.ReleaseBuffers = false;
            harness.Sink.OnReceived = buffer => delivered = buffer;

            harness.StartReceiver();
            harness.DeliverReceive(new byte[64]);

            Assert.That(delivered, Is.SameAs(rented[0]),
                "a full-buffer receive must hand the owner directly (no view allocation)");
            Assert.That(delivered!.Count, Is.EqualTo(64));
            Assert.That(rented[0].FinalReleaseCount, Is.EqualTo(0));

            delivered.Release();
            Assert.That(rented[0].FinalReleaseCount, Is.EqualTo(1));
        }

        [Test]
        public void PartialView_RangeGuards_AreMeaningful()
        {
            var core = new MultiRefByteArray(new byte[16]);

            Assert.Throws<ArgumentOutOfRangeException>(() => ReusablePartialView.Create(core, core.Count, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => ReusablePartialView.Create(core, -1, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => ReusablePartialView.Create(core, 4, 100));

            ReusablePartialView view = ReusablePartialView.Create(core, 2, 3);
            Assert.That(view.Count, Is.EqualTo(3));
            Assert.That(view.Offset, Is.EqualTo(2));
            Assert.Throws<IndexOutOfRangeException>(() => { _ = view[3]; });
            Assert.Throws<ArgumentOutOfRangeException>(() => view.CopyTo(new byte[8], 0, 2, 2));
            view.Release();
        }

        [Test]
        public void ReleasedView_ReportsUnderflowWithoutReviving()
        {
            var core = new MultiRefByteArray(new byte[16]);
            ReusablePartialView view = ReusablePartialView.Create(core, 0, 2);
            int underflows = 0;
            view.Error += (_, _) => underflows++;

            view.Release();
            view.Release();

            Assert.That(underflows, Is.EqualTo(1),
                "a release of an already-released view is reported through the counter diagnostics");
            Assert.That(view.IsAlive, Is.False);
            Assert.That(core.IsAlive, Is.True,
                "the underflowing release must not release the source a second time");
            core.Release();
        }

        private sealed class PartialReceiveAllocationFixture : IDisposable
        {
            private readonly byte[] _fullPayload;
            private readonly byte[] _partialPayload = { 1, 2, 3 };

            public PartialReceiveAllocationFixture(int bufferSize)
            {
                Pool = new ReusingReceivePool(bufferSize, 4);
                _fullPayload = new byte[bufferSize];
                Socket = new LowLevelTcpSocket(
                    Accessor,
                    Source,
                    Consumer.Handle,
                    Pool,
                    RemoteHalfClosePolicy.KeepSendOpen,
                    null,
                    bufferSize);
                Socket.Receiver.Start();
            }

            public FakeSocketAccessor Accessor { get; } = new();

            public ControllableQueue Source { get; } = new();

            public ReusingReceivePool Pool { get; }

            public NoCopyReceiveConsumer Consumer { get; } = new();

            public LowLevelTcpSocket Socket { get; }

            public void DeliverPartial()
            {
                TestSocketAsyncEventArgs args = Accessor.LastReceiveArgs
                                               ?? throw new InvalidOperationException("no receive is pending");
                Buffer.BlockCopy(_partialPayload, 0, args.Buffer!, args.Offset, _partialPayload.Length);
                args.Complete(_partialPayload.Length, SocketError.Success);
            }

            public void DeliverFull()
            {
                TestSocketAsyncEventArgs args = Accessor.LastReceiveArgs
                                               ?? throw new InvalidOperationException("no receive is pending");
                Buffer.BlockCopy(_fullPayload, 0, args.Buffer!, args.Offset, _fullPayload.Length);
                args.Complete(_fullPayload.Length, SocketError.Success);
            }

            public void Dispose()
            {
                Socket.Dispose();
                Accessor.CompletePendingOperations();
            }
        }
    }
}
