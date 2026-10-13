namespace Pontifex.Tests.NetSockets.LowLevelTcpSocketTests.Harness
{
    /// <summary>
    /// Validates that the test infrastructure itself is wired correctly (it exercises only current,
    /// expected-green behavior). The real behavioral suite is built on top of these fixtures.
    /// </summary>
    [LowLevelTcpSocketSuite]
    public class HarnessSanityTests
    {
        [Test]
        public void Send_TransfersBufferAndReleasesItOnceWhenDrained()
        {
            using var harness = new LowLevelSocketHarness();
            var buffer = new TrackingByteArray(new byte[] { 1, 2, 3 });
            harness.EnqueueSend(buffer);

            harness.Pump();

            Assert.That(harness.Accessor.SendCount, Is.EqualTo(1), "a send should be posted");
            Assert.That(buffer.ReleaseCount, Is.EqualTo(0), "buffer is still owned while the send is pending");

            harness.CompleteSend(3);

            Assert.That(buffer.ReleaseCount, Is.EqualTo(1), "buffer is released once fully sent");
        }

        [Test]
        public void Receive_DeliversBytesToTheSinkAndReArms()
        {
            using var harness = new LowLevelSocketHarness();

            harness.StartReceiver();

            Assert.That(harness.Accessor.ReceiveCount, Is.EqualTo(1), "the receiver should post a receive");

            harness.DeliverReceive(new byte[] { 9, 8, 7 });

            Assert.That(harness.Sink.CallCount, Is.EqualTo(1));
            Assert.That(harness.Sink.Received, Has.Length.EqualTo(1));
            Assert.That(harness.Sink.Received[0], Is.EqualTo(new byte[] { 9, 8, 7 }));
            Assert.That(harness.Accessor.ReceiveCount, Is.EqualTo(2), "the receiver should re-arm after dispatch");
        }
    }
}
