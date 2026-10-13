using System.Net.Sockets;
using Actuarius.Memory;

namespace Pontifex.Tests.NetSockets.LowLevelTcpSocketTests.Harness
{
    /// <summary>
    /// Scripted socket accessor and completion semantics: pending/post reuse, exactly-once completion,
    /// synchronous-vs-asynchronous results, and explicit bounded teardown of outstanding fake operations.
    /// </summary>
    [LowLevelTcpSocketSuite]
    public class FakeSocketAccessorTests
    {
        [Test]
        public void PendingArgs_CannotBePostedAgain()
        {
            var accessor = new FakeSocketAccessor { StrictMode = true };
            var args = (TestSocketAsyncEventArgs)accessor.CreateEventArgs();

            Assert.That(accessor.SendAsync(args), Is.True, "the first post stays pending");
            Assert.That(accessor.IsPending(args), Is.True);

            Assert.Throws<InvalidOperationException>(() => accessor.SendAsync(args),
                "posting args that already have an operation in flight must be rejected");
            Assert.That(accessor.Violations, Does.Contain(FakeContractViolation.DoublePost));

            Assert.That(accessor.IsPending(args), Is.True,
                "the rejection must not disturb the original pending operation");
        }

        [Test]
        public void CompletedArgs_CanBePostedAgain()
        {
            var accessor = new FakeSocketAccessor { StrictMode = true };
            var args = (TestSocketAsyncEventArgs)accessor.CreateEventArgs();

            Assert.That(accessor.SendAsync(args), Is.True);
            args.Complete(5);

            Assert.That(accessor.IsPending(args), Is.False,
                "in-use state must be cleared before the completion is dispatched, so a callback can repost");

            Assert.DoesNotThrow(() => accessor.SendAsync(args),
                "args are reusable once the previous operation has completed");
            Assert.That(accessor.IsPending(args), Is.True);
            Assert.That(accessor.CompletePendingOperations(), Is.EqualTo(1));
            Assert.That(accessor.Violations, Is.Empty);
        }

        [Test]
        public void DoubleCompletion_IsReportedInStrictMode()
        {
            var accessor = new FakeSocketAccessor { StrictMode = true };
            var args = (TestSocketAsyncEventArgs)accessor.CreateEventArgs();

            accessor.SendAsync(args);
            args.Complete(3);
            args.Complete(3);

            Assert.That(accessor.Violations, Does.Contain(FakeContractViolation.DoubleCompletion));
        }

        // ---------------------------------------------------------------------------------------
        // Native completion semantics: synchronous result vs asynchronous event.
        // ---------------------------------------------------------------------------------------

        [Test]
        public void SynchronousResult_DoesNotRaiseCompleted()
        {
            var accessor = new FakeSocketAccessor { StrictMode = true };
            var args = (TestSocketAsyncEventArgs)accessor.CreateEventArgs();
            bool raised = false;
            args.Completed += (_, _) => raised = true;

            accessor.OnSend = a =>
            {
                a.SetSynchronousResult(3);
                return false;
            };

            bool pending = accessor.SendAsync(args);

            Assert.That(pending, Is.False, "a synchronous result is reported by the false return");
            Assert.That(args.BytesTransferred, Is.EqualTo(3));
            Assert.That(args.CompletedCount, Is.EqualTo(0), "a synchronous result must not raise Completed");
            Assert.That(raised, Is.False);
            Assert.That(accessor.IsPending(args), Is.False);
            Assert.That(accessor.Violations, Is.Empty);

            // Conflating a synchronous result with an asynchronous completion must be flagged.
            var malformed = (TestSocketAsyncEventArgs)accessor.CreateEventArgs();
            accessor.OnSend = a =>
            {
                a.Complete(3);
                return false;
            };

            accessor.SendAsync(malformed);

            Assert.That(accessor.Violations, Does.Contain(FakeContractViolation.SynchronousResultRaisedEvent),
                "a false-returning post that raised Completed must be reported");
        }

        [Test]
        public void AsynchronousCompletion_RaisesExactlyOneCallback()
        {
            var accessor = new FakeSocketAccessor { StrictMode = true };
            var args = (TestSocketAsyncEventArgs)accessor.CreateEventArgs();
            int events = 0;
            args.Completed += (_, _) => Interlocked.Increment(ref events);

            Assert.That(accessor.SendAsync(args), Is.True);

            args.Complete(7, SocketError.Success);

            Assert.That(args.CompletedCount, Is.EqualTo(1));
            Assert.That(events, Is.EqualTo(1), "one asynchronous completion raises exactly one event");
            Assert.That(accessor.IsPending(args), Is.False);
            Assert.That(accessor.Violations, Is.Empty);
        }

        [Test]
        public void CompletionOnAnotherThreadBeforePostingReturns_IsObservedOnce()
        {
            var accessor = new FakeSocketAccessor { StrictMode = true };
            var args = (TestSocketAsyncEventArgs)accessor.CreateEventArgs();
            int events = 0;
            args.Completed += (_, _) => Interlocked.Increment(ref events);

            // Blocking handshake at the semantic boundary "completion raised, callback not yet
            // dispatched" — no thread-scheduling-dependent polling.
            var completionRaised = new ManualResetEventSlim(false);
            accessor.CompletionRaised += _ => completionRaised.Set();

            accessor.OnSend = a =>
            {
                var completion = new BackgroundAction(() => a.Complete(4, SocketError.Success), "stage1-completion");
                Assert.That(completionRaised.Wait(TestWait.DefaultTimeout), Is.True,
                    "the completion event must be raised before the posting call returns");
                completion.Join();
                return true;
            };

            bool pending = accessor.SendAsync(args);

            Assert.That(pending, Is.True);
            Assert.That(args.CompletedCount, Is.EqualTo(1));
            Assert.That(events, Is.EqualTo(1));
            Assert.That(accessor.IsPending(args), Is.False,
                "a post returning true must not re-mark an already completed operation pending");
            Assert.That(accessor.Violations, Is.Empty);
        }

        // ---------------------------------------------------------------------------------------
        // Pending-operation discovery and explicit bounded teardown.
        // ---------------------------------------------------------------------------------------

        [Test]
        public void Dispose_DoesNotAutoCompletePendingOperations()
        {
            using var harness = new LowLevelSocketHarness();
            harness.EnqueueSend(new byte[] { 1, 2, 3 });
            harness.Pump();

            Assert.That(harness.Accessor.PendingCount, Is.EqualTo(1));

            harness.Socket.Dispose();

            Assert.That(harness.Accessor.PendingCount, Is.EqualTo(1),
                "disposal must not fabricate a native completion");
        }

        [Test]
        public void BoundedTeardown_CompletesOutstandingOperations()
        {
            using var harness = new LowLevelSocketHarness();
            harness.EnqueueSend(new byte[] { 1, 2, 3 });
            harness.Pump();

            Assert.That(harness.Accessor.PendingCount, Is.EqualTo(1));

            int completed = harness.CompletePendingOperations();

            Assert.That(completed, Is.EqualTo(1));
            Assert.That(harness.Accessor.PendingCount, Is.EqualTo(0));
        }
    }
}
