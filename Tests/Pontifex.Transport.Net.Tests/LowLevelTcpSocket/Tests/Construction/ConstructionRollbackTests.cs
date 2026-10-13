using System.Net;
using System.Net.Sockets;
using Pontifex.NetSockets.Tcp;
using Pontifex.Tests.NetSockets.LowLevelTcpSocketTests.Harness;

namespace Pontifex.Tests.NetSockets.LowLevelTcpSocketTests
{
    /// <summary>
    /// Failed low-level construction must roll back the directional args it already created without
    /// disposing or touching the caller-owned accessor, source, or pool.
    /// </summary>
    [LowLevelTcpSocketSuite]
    public class ConstructionRollbackTests
    {
        private static LowLevelTcpSocket NewSocket(
            ISocketAccessor accessor,
            ControllableQueue queue,
            ControllablePool pool)
            => new LowLevelTcpSocket(accessor, queue, new ReceiveSink().Handle, pool);

        [Test]
        public void SecondArgsCreationFailure_DisposesFirstArgs_LeavesAccessorOwnedByCaller()
        {
            var accessor = new FakeSocketAccessor();
            var queue = new ControllableQueue();
            var pool = new ControllablePool();
            var initiating = new InvalidOperationException("second args creation failed");
            int attempts = 0;
            accessor.CreateEventArgsFactory = () =>
            {
                if (Interlocked.Increment(ref attempts) == 2)
                {
                    throw initiating;
                }

                return new TestSocketAsyncEventArgs();
            };

            var thrown = Assert.Throws<InvalidOperationException>(
                () => NewSocket(accessor, queue, pool));

            Assert.That(thrown, Is.SameAs(initiating),
                "the initiating construction failure must not be masked");

            TestSocketAsyncEventArgs firstArgs = accessor.EventArgs.Single();
            Assert.That(accessor.DisposeEventArgsCount, Is.EqualTo(1),
                "the successfully created first args must be disposed exactly once");
            Assert.That(firstArgs.IsDisposed, Is.True,
                "the real first args instance must actually be disposed");
            Assert.That(accessor.DisposeCount, Is.EqualTo(0),
                "the caller-owned accessor must survive construction failure");
            Assert.That(accessor.ShutdownCalls, Is.Empty,
                "construction failure must not request a socket shutdown");
            Assert.That(queue.PutCount, Is.EqualTo(0),
                "the caller-owned source must not be touched");
            Assert.That(pool.AcquireCount, Is.EqualTo(0),
                "the caller-owned pool must not be touched");

            // No handler may remain subscribed to the rolled-back args.
            Assert.DoesNotThrow(() => firstArgs.Complete(0, SocketError.OperationAborted),
                "a rolled-back args must not still be wired into the partially constructed transport");
        }

        [Test]
        public void FirstArgsCreationFailure_HasNoPartialOwnership()
        {
            var accessor = new FakeSocketAccessor();
            var queue = new ControllableQueue();
            var pool = new ControllablePool();
            var initiating = new NotSupportedException("first args creation failed");
            accessor.CreateEventArgsFactory = () => throw initiating;

            var thrown = Assert.Throws<NotSupportedException>(
                () => NewSocket(accessor, queue, pool));

            Assert.That(thrown, Is.SameAs(initiating));
            Assert.That(accessor.EventArgs, Is.Empty,
                "a throwing creation must not hand out a partial args");
            Assert.That(accessor.DisposeEventArgsCount, Is.EqualTo(0),
                "nothing was created, so nothing may be disposed");
            Assert.That(accessor.DisposeCount, Is.EqualTo(0),
                "the caller-owned accessor must survive construction failure");
            Assert.That(queue.PutCount, Is.EqualTo(0));
            Assert.That(pool.AcquireCount, Is.EqualTo(0));
        }
    }
}
