using Pontifex.NetSockets.Tcp;
using Pontifex.Tests.NetSockets.LowLevelTcpSocketTests.Harness;

namespace Pontifex.Tests.NetSockets.LowLevelTcpSocketTests
{
    [LowLevelTcpSocketSuite]
    public class LowLevelTcpSocketConstructionTests
    {
        [Test]
        public void NullSocket_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new LowLevelTcpSocket(null!, new ControllableQueue(), new ReceiveSink().Handle, new ControllablePool()));
        }

        [Test]
        public void NullSource_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new LowLevelTcpSocket(new FakeSocketAccessor(), null!, new ReceiveSink().Handle, new ControllablePool()));
        }

        [Test]
        public void NullOnReceived_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new LowLevelTcpSocket(new FakeSocketAccessor(), new ControllableQueue(), null!, new ControllablePool()));
        }

        [Test]
        public void NullPool_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new LowLevelTcpSocket(new FakeSocketAccessor(), new ControllableQueue(), new ReceiveSink().Handle, null!));
        }

        [Test]
        public void InvalidPolicy_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new LowLevelTcpSocket(
                    new FakeSocketAccessor(),
                    new ControllableQueue(),
                    new ReceiveSink().Handle,
                    new ControllablePool(),
                    (RemoteHalfClosePolicy)999));
        }

        [Test]
        public void NonPositiveReceiveBufferSize_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new LowLevelTcpSocket(
                    new FakeSocketAccessor(),
                    new ControllableQueue(),
                    new ReceiveSink().Handle,
                    new ControllablePool(),
                    receiveBufferSize: 0));
        }

        [Test]
        public void Endpoints_AreExposed()
        {
            using var harness = new LowLevelSocketHarness();

            Assert.That(harness.Sender, Is.Not.Null);
            Assert.That(harness.Receiver, Is.Not.Null);
            Assert.That(harness.Socket.Completion, Is.Not.Null);
        }
    }
}
