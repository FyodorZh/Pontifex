using System.Net.Sockets;
using Actuarius.Memory;

namespace Pontifex.Tests.NetSockets.LowLevelTcpSocketTests.Harness
{
    /// <summary>
    /// Ownership-tracking buffer double: final release, release-call and underflow accounting
    /// pinned against the installed Actuarius.Memory reference-count diagnostics.
    /// </summary>
    [LowLevelTcpSocketSuite]
    public class TrackingByteArrayTests
    {
        [Test]
        public void DoubleRelease_IsDetected_EvenWhenFinalReleaseCountIsOne()
        {
            var buffer = new TrackingByteArray(new byte[] { 1, 2, 3 });

            buffer.Release();
            buffer.Release();

            Assert.That(buffer.FinalReleaseCount, Is.EqualTo(1),
                "only the first release reaches the final-reference transition");
            Assert.That(buffer.ReleaseCallCount, Is.EqualTo(2),
                "both release calls must be counted, including the failed one");
            Assert.That(buffer.UnderflowCount, Is.EqualTo(1),
                "the second release must be reported as an underflow by the ref-count diagnostics");
            Assert.That(buffer.CurrentRefCount, Is.EqualTo(-1),
                "Actuarius.Memory does not clamp the reference count at zero");
            Assert.That(buffer.HasMisuse, Is.True);
        }

        [Test]
        public void BalancedAcquireRelease_RecordsSingleFinalReleaseAndZeroRefs()
        {
            var buffer = new TrackingByteArray(new byte[] { 1, 2, 3 });

            buffer.Release();

            Assert.That(buffer.FinalReleaseCount, Is.EqualTo(1));
            Assert.That(buffer.ReleaseCallCount, Is.EqualTo(1));
            Assert.That(buffer.UnderflowCount, Is.EqualTo(0));
            Assert.That(buffer.CurrentRefCount, Is.EqualTo(0));
            Assert.That(buffer.HasMisuse, Is.False);
        }
    }
}
