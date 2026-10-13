using Pontifex.Tests.NetSockets.LowLevelTcpSocketTests.Harness;

namespace Pontifex.Tests.NetSockets.LowLevelTcpSocketTests
{
    /// <summary>
    /// Each SourceDrained handler is admitted atomically against terminal state. No handler
    /// is admitted after termination; an already-admitted handler may finish or enter later;
    /// Completion does not wait for handlers.
    /// </summary>
    [LowLevelTcpSocketSuite]
    public class DrainHandlerAdmissionTests
    {
        [Test]
        public void FirstHandlerTerminates_SkipsSecondHandler()
        {
            using var harness = new LowLevelSocketHarness();
            int firstCalls = 0;
            int secondCalls = 0;
            harness.Sender.SourceDrained += () =>
            {
                firstCalls++;
                harness.ShutdownSender(true);
            };
            harness.Sender.SourceDrained += () => secondCalls++;

            harness.Pump();

            Assert.That(firstCalls, Is.EqualTo(1));
            Assert.That(secondCalls, Is.EqualTo(0),
                "no handler may be admitted after the sender became terminal");
        }

        [Test]
        public void AbortBeforeAdmission_SuppressesHandler()
        {
            using var harness = new LowLevelSocketHarness();
            int calls = 0;
            harness.Sender.SourceDrained += () => calls++;

            var atAdmission = new ManualResetEventSlim(false);
            var release = new ManualResetEventSlim(false);
            bool paused = false;
            harness.Socket.BeforeDrainHandlerAdmission = index =>
            {
                if (index == 0 && !paused)
                {
                    paused = true;
                    atAdmission.Set();
                    release.Wait(TestWait.DefaultTimeout);
                }
            };

            var pump = new BackgroundAction(() => harness.Pump(), "stage9-pump");
            try
            {
                Assert.That(atAdmission.Wait(TestWait.DefaultTimeout), Is.True,
                    "the notifier must reach the handler admission boundary");
                harness.Socket.Dispose();
            }
            finally
            {
                release.Set();
                pump.Join();
                harness.Socket.BeforeDrainHandlerAdmission = null;
            }

            Assert.That(calls, Is.EqualTo(0),
                "a handler must not be admitted once the sender is terminal");
        }

        [Test]
        public void AlreadyAdmittedHandler_MayCompleteAfterAbort()
        {
            using var harness = new LowLevelSocketHarness();
            int firstCalls = 0;
            int secondCalls = 0;
            harness.Sender.SourceDrained += () => firstCalls++;
            harness.Sender.SourceDrained += () => secondCalls++;

            var afterAdmission = new ManualResetEventSlim(false);
            var release = new ManualResetEventSlim(false);
            harness.Socket.BeforeDrainHandlerInvocation = index =>
            {
                if (index == 0)
                {
                    afterAdmission.Set();
                    release.Wait(TestWait.DefaultTimeout);
                }
            };

            var pump = new BackgroundAction(() => harness.Pump(), "stage9-pump");
            try
            {
                Assert.That(afterAdmission.Wait(TestWait.DefaultTimeout), Is.True,
                    "the first handler must be admitted and paused before invocation");

                harness.Socket.Dispose();

                Assert.That(harness.Sender.Completion.IsCompleted, Is.True,
                    "Completion must not wait for an admitted drain handler");
            }
            finally
            {
                release.Set();
                pump.Join();
                harness.Socket.BeforeDrainHandlerInvocation = null;
            }

            Assert.That(firstCalls, Is.EqualTo(1),
                "an already-admitted handler may finish after the abort");
            Assert.That(secondCalls, Is.EqualTo(0),
                "a later handler must not be admitted after termination");
        }

        [Test]
        public void ThrowingFirstHandler_LaterHandlersStillRun()
        {
            using var harness = new LowLevelSocketHarness();
            harness.Sender.SourceDrained += () => throw new InvalidOperationException("boom");
            int laterCalls = 0;
            harness.Sender.SourceDrained += () => laterCalls++;

            harness.Pump();

            Assert.That(harness.CallbackErrors, Has.Count.EqualTo(1));
            Assert.That(harness.CallbackErrors[0].Message, Is.EqualTo("boom"));
            Assert.That(laterCalls, Is.EqualTo(1),
                "per-handler exception isolation must not skip later eligible handlers");
        }

        [Test]
        public void DisposeFromHandler_DoesNotDeadlock()
        {
            using var harness = new LowLevelSocketHarness();
            int calls = 0;
            harness.Sender.SourceDrained += () =>
            {
                calls++;
                harness.Socket.Dispose();
            };

            Assert.DoesNotThrow(() => harness.Pump(),
                "a handler that disposes the socket must not deadlock on an admission gate");
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(harness.Sender.Completion.IsCompleted, Is.True);
        }

        [Test]
        public void HandlerRepump_IsNonRecursive()
        {
            using var harness = new LowLevelSocketHarness();
            int depth = 0;
            int maxDepth = 0;
            int calls = 0;
            harness.Sender.SourceDrained += () =>
            {
                calls++;
                maxDepth = Math.Max(maxDepth, ++depth);
                harness.Pump(); // nested drain request while a handler is running
                depth--;
            };

            harness.Pump();

            Assert.That(calls, Is.GreaterThanOrEqualTo(1));
            Assert.That(maxDepth, Is.EqualTo(1), "drain notifications must not recurse");
        }
    }
}
