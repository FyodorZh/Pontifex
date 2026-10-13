using System.Net.Sockets;
using System.Runtime.CompilerServices;
using Actuarius.Memory;
using Pontifex.NetSockets.Tcp;
using Pontifex.Tests.NetSockets.LowLevelTcpSocketTests.Harness;

namespace Pontifex.Tests.NetSockets.LowLevelTcpSocketTests
{
    /// <summary>
    /// Event-args buffer roots are cleared at safe boundaries. Structural assertions are primary: a
    /// finished/idle send, and a terminal cleanup in either direction (normal or late after forced
    /// publication), must not retain released storage through <see cref="SocketAsyncEventArgs"/>.
    /// Positive controls keep partial-progress ranges installed and keep native-pending storage
    /// alive; a consumer-retained receive view stays valid after transport detachment. A separate
    /// retention probe uses a non-inlined helper, <see cref="WeakReference"/>, explicit collection,
    /// <see cref="GC.KeepAlive(object)"/>, and a non-retaining pool.
    /// </summary>
    [LowLevelTcpSocketSuite]
    public class EventArgsStorageTests
    {
        [Test]
        public void DrainedIdleSender_DoesNotKeepReleasedArrayInArgs()
        {
            using var harness = new LowLevelSocketHarness();
            var buffer = new TrackingByteArray(new byte[64 * 1024]);
            harness.EnqueueSend(buffer);
            harness.Pump();

            TestSocketAsyncEventArgs args = harness.LastSend!;
            Assert.That(args.Buffer, Is.Not.Null, "precondition: the send installed its buffer");

            harness.CompleteSend(buffer.Count);

            Assert.That(buffer.FinalReleaseCount, Is.EqualTo(1),
                "the transport releases its in-flight buffer when the send completes");
            Assert.That(buffer.HasMisuse, Is.False);
            AssertArgsDetached(args,
                "a drained idle sender must not retain its last released array through the event args");
        }

        [Test]
        public void DrainedIdleSender_ReleasedArrayBecomesCollectible()
        {
            using var harness = new LowLevelSocketHarness(pool: new ControllablePool());

            WeakReference weak = SendLargeBufferAndDrain(harness);

            GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
            GC.WaitForPendingFinalizers();
            GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);

            Assert.That(weak.IsAlive, Is.False,
                "a released array must not stay rooted through the idle event args");
            GC.KeepAlive(harness.Socket);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static WeakReference SendLargeBufferAndDrain(LowLevelSocketHarness harness)
        {
            byte[] array = new byte[1024 * 1024];
            harness.EnqueueSend(new MultiRefByteArray(array));
            harness.Pump();
            harness.CompleteSend(array.Length);
            return new WeakReference(array);
        }

        [Test]
        public void TerminalCleanup_DetachesReleasedStorage()
        {
            // Normal completion: both directions.
            using (var harness = new LowLevelSocketHarness())
            {
                var sendBuffer = new TrackingByteArray(new byte[] { 1, 2, 3, 4 });
                harness.EnqueueSend(sendBuffer);
                harness.Pump();
                TestSocketAsyncEventArgs sendArgs = harness.LastSend!;
                harness.CompleteSend(sendBuffer.Count);
                Assert.That(sendBuffer.FinalReleaseCount, Is.EqualTo(1));
                AssertArgsDetached(sendArgs,
                    "sender normal completion must detach released storage");

                TrackingByteArray? receiveBuffer = null;
                harness.Controllable!.Factory = _ => receiveBuffer = new TrackingByteArray(new byte[16 * 1024]);
                harness.StartReceiver();
                TestSocketAsyncEventArgs receiveArgs = harness.LastReceive!;
                harness.DeliverReceive(new byte[] { 9, 8, 7 });

                harness.Receiver.Shutdown();
                harness.CompleteReceive(0);

                Assert.That(receiveBuffer, Is.Not.Null);
                Assert.That(receiveBuffer!.FinalReleaseCount, Is.EqualTo(1),
                    "the re-armed in-flight receive buffer is reclaimed on terminal cleanup");
                AssertArgsDetached(receiveArgs,
                    "receiver normal terminal cleanup must detach released storage");
            }

            // Late cleanup after forced publication: both directions.
            var executor = new ManualTeardownExecutor();
            using (var harness = new LowLevelSocketHarness(teardownExecutor: executor))
            {
                var sendBuffer = new TrackingByteArray(new byte[] { 1, 2, 3 });
                harness.EnqueueSend(sendBuffer);
                harness.Pump();
                TestSocketAsyncEventArgs sendArgs = harness.LastSend!;

                TrackingByteArray? receiveBuffer = null;
                harness.Controllable!.Factory = _ => receiveBuffer = new TrackingByteArray(new byte[16 * 1024]);
                harness.StartReceiver();
                TestSocketAsyncEventArgs receiveArgs = harness.LastReceive!;

                harness.Socket.ForceAbortFinalize(new TimeoutException("stage16 forced"));
                executor.Run();

                harness.CompleteSend(0, SocketError.OperationAborted);
                harness.CompleteReceive(0, SocketError.OperationAborted);

                Assert.That(sendBuffer.FinalReleaseCount, Is.EqualTo(1));
                Assert.That(receiveBuffer!.FinalReleaseCount, Is.EqualTo(1));
                AssertArgsDetached(sendArgs,
                    "late sender cleanup after forced publication must detach released storage");
                AssertArgsDetached(receiveArgs,
                    "late receiver cleanup after forced publication must detach released storage");
            }
        }

        [Test]
        public void PartialSend_KeepsRemainingBufferInstalled()
        {
            using var harness = new LowLevelSocketHarness();
            var buffer = new TrackingByteArray(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });
            harness.EnqueueSend(buffer);
            harness.Pump();

            harness.CompleteSend(4);

            TestSocketAsyncEventArgs args = harness.LastSend!;
            Assert.That(buffer.FinalReleaseCount, Is.EqualTo(0),
                "the remaining range is still in flight and must not be released");
            Assert.That(args.Buffer, Is.SameAs(buffer.Array),
                "a partial send must keep the remaining range installed");
            Assert.That(args.Offset, Is.EqualTo(4));
            Assert.That(args.Count, Is.EqualTo(6));

            harness.CompleteSend(6);

            Assert.That(buffer.FinalReleaseCount, Is.EqualTo(1));
            AssertArgsDetached(args, "the completed partial send must detach its released storage");
        }

        [Test]
        public void NativePendingOperations_KeepArgsStorageThroughForcePublication()
        {
            var executor = new ManualTeardownExecutor();
            using var harness = new LowLevelSocketHarness(teardownExecutor: executor);

            var sendBuffer = new TrackingByteArray(new byte[] { 1, 2, 3 });
            harness.EnqueueSend(sendBuffer);
            harness.Pump();

            TrackingByteArray? receiveBuffer = null;
            harness.Controllable!.Factory = _ => receiveBuffer = new TrackingByteArray(new byte[1024]);
            harness.StartReceiver();

            TestSocketAsyncEventArgs sendArgs = harness.LastSend!;
            TestSocketAsyncEventArgs receiveArgs = harness.LastReceive!;

            harness.Socket.ForceAbortFinalize(new TimeoutException("stage16 native-pending"));

            Assert.That(sendArgs.Buffer, Is.Not.Null,
                "a native-pending send must keep its storage installed through force publication");
            Assert.That(receiveArgs.Buffer, Is.Not.Null,
                "a native-pending receive must keep its storage installed through force publication");
            Assert.That(sendBuffer.FinalReleaseCount, Is.EqualTo(0),
                "native-pending send storage must stay alive");
            Assert.That(receiveBuffer!.FinalReleaseCount, Is.EqualTo(0),
                "native-pending receive storage must stay alive");

            executor.Run();
            harness.CompleteSend(0, SocketError.OperationAborted);
            harness.CompleteReceive(0, SocketError.OperationAborted);

            Assert.That(sendBuffer.FinalReleaseCount, Is.EqualTo(1));
            Assert.That(receiveBuffer.FinalReleaseCount, Is.EqualTo(1));
            AssertArgsDetached(sendArgs, "late sender cleanup must detach the released storage");
            AssertArgsDetached(receiveArgs, "late receiver cleanup must detach the released storage");
        }

        [Test]
        public void ConsumerRetainedReceive_StaysValidAfterTransportDetachment()
        {
            var executor = new ManualTeardownExecutor();
            using var harness = new LowLevelSocketHarness(teardownExecutor: executor);

            var rentedBuffers = new List<TrackingByteArray>();
            harness.Controllable!.Factory = _ =>
            {
                var rented = new TrackingByteArray(new byte[16 * 1024]);
                rentedBuffers.Add(rented);
                return rented;
            };

            IMultiRefByteArray? retained = null;
            harness.Sink.OnReceived = buffer =>
            {
                retained = buffer;
                buffer.AddRef();
            };

            harness.StartReceiver();
            harness.DeliverReceive(new byte[] { 7, 6, 5 });

            Assert.That(retained, Is.Not.Null);
            Assert.That(rentedBuffers, Has.Count.EqualTo(2),
                "precondition: the delivered buffer and the re-armed receive buffer were acquired");

            harness.Socket.ForceAbortFinalize(new TimeoutException("stage16 retained view"));
            executor.Run();
            harness.CompleteReceive(0, SocketError.OperationAborted);

            Assert.That(harness.LastReceive!.Buffer, Is.Null,
                "the transport must detach its released storage after late cleanup");
            Assert.That(retained!.Count, Is.EqualTo(3),
                "a consumer-retained view must survive transport detachment");
            Assert.That(retained[0], Is.EqualTo(7));
            Assert.That(retained[1], Is.EqualTo(6));
            Assert.That(retained[2], Is.EqualTo(5));

            retained.Release();

            Assert.That(rentedBuffers[0].FinalReleaseCount, Is.EqualTo(1),
                "releasing the retained view must return the underlying rented buffer exactly once");
            Assert.That(rentedBuffers[0].HasMisuse, Is.False);
            Assert.That(rentedBuffers[1].FinalReleaseCount, Is.EqualTo(1),
                "the re-armed buffer is reclaimed by the late native completion");
        }

        private static void AssertArgsDetached(TestSocketAsyncEventArgs? args, string message)
        {
            Assert.That(args, Is.Not.Null);
            Assert.That(args!.Buffer, Is.Null.Or.Empty, message);
        }
    }
}
