using System.Net.Sockets;
using Pontifex.NetSockets.Tcp;
using Pontifex.Tests.NetSockets.LowLevelTcpSocketTests.Harness;

namespace Pontifex.Tests.NetSockets.LowLevelTcpSocketTests
{
    [LowLevelTcpSocketSuite]
    public class SourceDrainedTests
    {
        [Test]
        public void EdgeTriggered_OncePerDrain()
        {
            using var harness = new LowLevelSocketHarness();
            int drained = 0;
            harness.Sender.SourceDrained += () => drained++;

            harness.Pump();
            harness.Pump();

            Assert.That(drained, Is.EqualTo(1));
        }

        [Test]
        public void ReEligible_AfterSuccessfulPopAndDrain()
        {
            using var harness = new LowLevelSocketHarness();
            int drained = 0;
            harness.Sender.SourceDrained += () => drained++;

            harness.Pump();
            Assert.That(drained, Is.EqualTo(1));

            harness.EnqueueSend(new byte[] { 1, 2 });
            harness.Pump();
            harness.CompleteSend(2);

            Assert.That(drained, Is.EqualTo(2));
        }

        [Test]
        public void HandlerException_RoutedToCallbackError()
        {
            using var harness = new LowLevelSocketHarness();
            harness.Sender.SourceDrained += () => throw new InvalidOperationException("handler");

            harness.Pump();

            Assert.That(harness.CallbackErrors, Has.Count.EqualTo(1));
        }

        [Test]
        public void SourceDrainedHandler_RePumpingIsNotRecursive()
        {
            using var harness = new LowLevelSocketHarness();
            const int iterations = 64;
            int published = 0;
            int depth = 0;
            int maxDepth = 0;

            harness.Sender.SourceDrained += () =>
            {
                depth++;
                maxDepth = Math.Max(maxDepth, depth);
                if (published < iterations)
                {
                    published++;
                    harness.EnqueueSend(TestBuffers.Empty());
                    harness.Pump();
                }

                depth--;
            };

            harness.Pump();

            Assert.That(published, Is.EqualTo(iterations));
            Assert.That(maxDepth, Is.EqualTo(1),
                "nested SourceDrained notifications must be trampolined, not recursive");
        }

        [Test]
        public void NestedDrains_CoalesceIntoOnePendingNotification()
        {
            using var harness = new LowLevelSocketHarness();
            int calls = 0;

            harness.Sender.SourceDrained += () =>
            {
                calls++;
                if (calls != 1)
                {
                    return;
                }

                harness.EnqueueSend(TestBuffers.Empty());
                harness.Pump();
                harness.EnqueueSend(TestBuffers.Empty());
                harness.Pump();
            };

            harness.Pump();

            Assert.That(calls, Is.EqualTo(2),
                "nested drain notifications must coalesce into a single pending notification");
        }

        [Test]
        public void ThrowingSourceDrainedHandler_IsReportedAndDoesNotDisrupt()
        {
            using var harness = new LowLevelSocketHarness();
            int calls = 0;
            harness.Sender.SourceDrained += () =>
            {
                calls++;
                throw new InvalidOperationException("boom");
            };

            harness.Pump();

            Assert.That(calls, Is.EqualTo(1));
            Assert.That(harness.CallbackErrors, Has.Count.EqualTo(1));
            Assert.That(harness.CallbackErrors[0].Message, Is.EqualTo("boom"));

            harness.Pump();
            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void ThrowingDiagnosticCallback_IsSwallowed()
        {
            using var harness = new LowLevelSocketHarness(
                onCallbackError: _ => throw new InvalidOperationException("diag"));
            harness.Sender.SourceDrained += () => throw new InvalidOperationException("handler");

            harness.Pump();

            Assert.That(harness.CallbackErrors, Has.Count.EqualTo(1));
        }
    }
}
