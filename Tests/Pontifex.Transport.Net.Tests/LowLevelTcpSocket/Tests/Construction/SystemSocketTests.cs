using System.Net;
using System.Net.Sockets;
using Pontifex.NetSockets.Tcp;
using Pontifex.Tests.NetSockets.LowLevelTcpSocketTests.Harness;

namespace Pontifex.Tests.NetSockets.LowLevelTcpSocketTests
{
    [LowLevelTcpSocketSuite]
    public class SystemSocketTests
    {
        [Test]
        public void Rejects_NonTcpSocket()
        {
            using var udp = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

            Assert.Throws<ArgumentException>(() => new SystemSocket(udp));
            Assert.That(udp.SafeHandle.IsClosed, Is.False,
                "rejection must not dispose the caller's socket");
        }

        [Test]
        public void Rejects_UnconnectedTcpSocket()
        {
            using var tcp = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            Assert.Throws<ArgumentException>(() => new SystemSocket(tcp));
            Assert.That(tcp.SafeHandle.IsClosed, Is.False,
                "rejection must not dispose the caller's socket");
        }

        [Test]
        public void Rejects_DisposedSocket()
        {
            var disposed = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            disposed.Dispose();

            Assert.Throws<ObjectDisposedException>(() => new SystemSocket(disposed));
        }

        [Test]
        public void Rejects_NullSocket()
        {
            Assert.Throws<ArgumentNullException>(() => new SystemSocket(null!));
        }

        [Test]
        public void Accepts_ConnectedLoopbackSocket()
        {
            using var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            listener.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            listener.Listen(1);

            using var client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            client.Connect(listener.LocalEndPoint!);
            Assert.That(client.Connected, Is.True, "precondition: the loopback client is connected");

            using var accessor = new SystemSocket(client);

            Assert.That(accessor, Is.Not.Null);
            Assert.That(client.SafeHandle.IsClosed, Is.False,
                "accepted ownership must not dispose the socket at construction");
        }
    }
}
