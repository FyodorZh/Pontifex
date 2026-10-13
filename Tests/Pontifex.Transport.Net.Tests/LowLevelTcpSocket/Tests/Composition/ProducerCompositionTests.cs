using System.Net.Sockets;
using Actuarius.Collections;
using Actuarius.Memory;
using Pontifex.NetSockets.Tcp;
using Pontifex.Tests.NetSockets.LowLevelTcpSocketTests.Harness;

namespace Pontifex.Tests.NetSockets.LowLevelTcpSocketTests
{
    /// <summary>
    /// Executable producer-composition contract: seal, request shutdown, and drain only after
    /// SourceQuiesced; strict single-writer publication; and fixture teardown that reclaims borrowed
    /// buffers through outstanding fake native operations.
    /// </summary>
    [LowLevelTcpSocketSuite]
    public class ProducerCompositionTests
    {
        // ---------------------------------------------------------------------------------------
        // Executable composition contract (test-local helper fixture)
        // ---------------------------------------------------------------------------------------

        /// <summary>
        /// Test-local composition helper: the documented producer handoff. Publication is sealed
        /// first, then shutdown is requested; the producer drains only after the transport
        /// acknowledges reader quiescence (logical completion is not a drain-safe handoff).
        /// </summary>
        private static class ProducerHandoff
        {
            public static async Task DrainAsync(
                ITcpSocketSender sender,
                ISingleReaderWriterConcurrentQueue<IMultiRefByteArray> source,
                Action sealPublication,
                Action? onDrainEnter = null)
            {
                // Single writer: stop publishing new items.
                sealPublication();

                // Graceful send-side shutdown; explicit parameterless overload uses the ctor default.
                sender.Shutdown(true);

                // Logical completion for result/diagnostics (may resolve while a read is blocked).
                await sender.Completion.ConfigureAwait(false);

                // Required before any other reader touches the source: reader quiescence.
                await sender.SourceQuiesced.ConfigureAwait(false);

                // Safe now: drain and release the producer-owned queued-but-unsent buffers.
                onDrainEnter?.Invoke();
                while (source.TryPop(out IMultiRefByteArray? leftover))
                {
                    leftover.Release();
                }
            }
        }

        [Test]
        public async Task ProducerHandoffHelper_WaitsForSourceQuiesced()
        {
            using var harness = new LowLevelSocketHarness();
            harness.Queue.StrictMode = true;
            harness.Queue.Pause();
            var popEntered = new ManualResetEventSlim(false);
            harness.Queue.OnPopEntered = () => popEntered.Set();

            var pump = new BackgroundAction(() => harness.Pump(), "stage15-handoff-pump");
            bool publicationSealed = false;
            bool? quiescedAtDrainEntry = null;
            var drainEntered = new ManualResetEventSlim(false);
            Task? handoff = null;
            try
            {
                Assert.That(popEntered.Wait(TestWait.DefaultTimeout), Is.True,
                    "precondition: the transport must be inside the reserved source read");

                handoff = ProducerHandoff.DrainAsync(
                    harness.Sender,
                    harness.Queue,
                    () => publicationSealed = true,
                    () =>
                    {
                        quiescedAtDrainEntry = harness.Sender.SourceQuiesced.IsCompleted;
                        drainEntered.Set();
                    });

                Assert.That(publicationSealed, Is.True,
                    "the producer must seal publication before requesting shutdown");

                // Bounded termination publishes logical completion while the read is still blocked.
                harness.Socket.ForceAbortFinalize(new TimeoutException("stage15 handoff"));
                await TestWait.WaitCompletionAsync(harness.Sender.Completion);

                // A legacy Completion-only helper enters its drain here, while the source read is
                // still blocked; the corrected helper is still waiting for reader quiescence.
                drainEntered.Wait(TimeSpan.FromMilliseconds(300));

                Assert.That(harness.Sender.SourceQuiesced.IsCompleted, Is.False,
                    "the handoff must not proceed while the transport is still inside the source read");
                Assert.That(handoff.IsCompleted, Is.False,
                    "the helper must wait for SourceQuiesced, not merely for Completion");

                harness.Queue.Resume();
                await TestWait.WaitCompletionAsync(handoff);
            }
            finally
            {
                harness.Queue.Resume();
                pump.Join();
                harness.Queue.OnPopEntered = null;
            }

            Assert.That(quiescedAtDrainEntry, Is.True,
                "the producer drain must begin only after SourceQuiesced resolves");
            Assert.That(harness.Queue.ReaderOverlapCount, Is.EqualTo(0),
                "the producer drain must not overlap the transport's source reader");
            Assert.That(harness.Queue.ActiveReaders, Is.EqualTo(0));
        }

        /// <summary>
        /// Test-local fixture whose teardown is part of the contract under test: it must complete
        /// outstanding fake native operations (so borrowed buffers are returned through the late
        /// native completion path) and then wait bounded for the guarded pool to balance.
        /// </summary>
        private sealed class SocketFixture : IDisposable
        {
            public SocketFixture(ByteArrayConcurrentPoolGuard pool)
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

        [Test]
        public void FixtureTeardown_CompletesOutstandingFakeOperations()
        {
            var pool = TestPools.GuardedRealPool();
            var fixture = new SocketFixture(pool);
            fixture.Harness.StartReceiver();

            Assert.That(fixture.Harness.Accessor.PendingCount, Is.EqualTo(1),
                "precondition: a receive is armed and natively pending with a borrowed buffer");

            fixture.Dispose();

            Assert.That(fixture.Harness.Accessor.PendingCount, Is.EqualTo(0),
                "fixture teardown must not leave outstanding fake native operations");
            pool.AssertNoOutstanding();
            Assert.That(pool.ErrorCount, Is.EqualTo(0),
                "the late native completion must return the borrowed buffer through the guarded pool");
        }

        [Test]
        public void NativePendingProbe_RetainsBorrowedBufferUntilLateCompletion()
        {
            // Deliberate native-pending probe, kept separate from fixture teardown: while a native
            // operation still references the buffer, the transport must NOT reclaim it even after
            // logical teardown. The probe accounts for the resource before finishing.
            using var harness = new LowLevelSocketHarness();
            TrackingByteArray? receiveBuffer = null;
            harness.Controllable!.Factory = _ => receiveBuffer = new TrackingByteArray(new byte[16 * 1024]);
            harness.StartReceiver();
            Assert.That(harness.Accessor.PendingCount, Is.EqualTo(1));

            harness.Socket.Dispose();

            Assert.That(receiveBuffer, Is.Not.Null);
            Assert.That(receiveBuffer!.FinalReleaseCount, Is.EqualTo(0),
                "storage referenced by a still-pending native operation must not be reclaimed early");

            harness.CompletePendingOperations();

            Assert.That(receiveBuffer.FinalReleaseCount, Is.EqualTo(1),
                "the late native completion must reclaim the borrowed buffer exactly once");
            Assert.That(receiveBuffer.HasMisuse, Is.False);
        }

        [Test]
        public void StrictSingleWriter_RejectsConcurrentProducerPublication()
        {
            var queue = new StrictSingleWriterQueue();

            // Valid single-writer example: sequential publication and one reader.
            var first = TestBuffers.Of(1, 2, 3);
            Assert.That(queue.Put(first), Is.True);
            Assert.That(queue.TryPop(out IMultiRefByteArray popped), Is.True);
            Assert.That(popped, Is.SameAs(first));
            Assert.That(queue.WriterViolations, Is.EqualTo(0));
            Assert.That(queue.ReaderViolations, Is.EqualTo(0));
            popped.Release();

            var writerInside = new ManualResetEventSlim(false);
            var releaseWriter = new ManualResetEventSlim(false);
            queue.OnWriteEntered = () =>
            {
                writerInside.Set();
                releaseWriter.Wait(TestWait.DefaultTimeout);
            };

            var bufferA = TestBuffers.Of(4);
            var bufferB = TestBuffers.Of(5);
            var writerA = new BackgroundAction(() => queue.Put(bufferA), "strict-writer-a");
            BackgroundAction? writerB = null;
            try
            {
                Assert.That(writerInside.Wait(TestWait.DefaultTimeout), Is.True,
                    "the first writer must be inside Put");

                writerB = new BackgroundAction(() => queue.Put(bufferB), "strict-writer-b");
                writerB.JoinRaw();

                Assert.That(writerB.Exception, Is.InstanceOf<InvalidOperationException>(),
                    "concurrent producer publication must be rejected by the strict source");
                Assert.That(queue.WriterViolations, Is.EqualTo(1));
            }
            finally
            {
                releaseWriter.Set();
                writerA.Join();
                queue.OnWriteEntered = null;
            }

            Assert.That(queue.TryPop(out IMultiRefByteArray published), Is.True);
            Assert.That(published, Is.SameAs(bufferA));
            published.Release();
            bufferB.Release();
        }
    }
}
