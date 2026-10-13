using System.Net;
using System.Net.Sockets;
using Actuarius.Collections;
using Actuarius.Memory;
using Pontifex.NetSockets.Tcp;

namespace Pontifex.Tests.NetSockets.LowLevelTcpSocketTests.Harness
{
    /// <summary>
    /// A loopback, real-socket integration fixture for the transport: a connected TCP pair (the
    /// transport owns one end through <see cref="SystemSocket"/>, the test drives the other end as a
    /// raw peer), the real Actuarius SPSC source, the installed shared byte-array pool wrapped in a
    /// leak-detecting guard, and a releasing receive consumer. Every resource is released by
    /// <see cref="Dispose"/>; tests assert guarded-pool balance explicitly.
    /// </summary>
    internal sealed class RealSocketFixture : IDisposable
    {
        private readonly Socket _transportSocket;

        private RealSocketFixture(
            Socket transportSocket,
            Socket peer,
            SingleReaderWriterConcurrentQueue<IMultiRefByteArray> source,
            ByteArrayConcurrentPoolGuard pool,
            RealReceiveConsumer consumer,
            LowLevelTcpSocket transport,
            ObservingSocketAccessor accessor)
        {
            _transportSocket = transportSocket;
            Peer = peer;
            Source = source;
            Pool = pool;
            Consumer = consumer;
            Transport = transport;
            Accessor = accessor;
        }

        /// <summary>The raw peer endpoint; tests send/read bytes and half-close/reset it.</summary>
        public Socket Peer { get; }

        /// <summary>The real SPSC source; the test is the single writer.</summary>
        public SingleReaderWriterConcurrentQueue<IMultiRefByteArray> Source { get; }

        /// <summary>Guarded view over the installed shared pool; own acquisitions must balance.</summary>
        public ByteArrayConcurrentPoolGuard Pool { get; }

        public RealReceiveConsumer Consumer { get; }

        public LowLevelTcpSocket Transport { get; }

        public ObservingSocketAccessor Accessor { get; }

        public static RealSocketFixture Create(
            int receiveBufferSize = 16 * 1024,
            int? transportSendBufferSize = null,
            RemoteHalfClosePolicy policy = RemoteHalfClosePolicy.KeepSendOpen)
        {
            var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            Socket? transportSocket = null;
            Socket? peer = null;
            try
            {
                listener.Bind(new IPEndPoint(IPAddress.Loopback, 0));
                listener.Listen(1);

                peer = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                peer.Connect(listener.LocalEndPoint!);
                transportSocket = listener.Accept();
            }
            finally
            {
                listener.Dispose();
                if (transportSocket == null)
                {
                    peer?.Dispose();
                }
            }

            transportSocket.NoDelay = true;
            if (transportSendBufferSize.HasValue)
            {
                transportSocket.SendBufferSize = transportSendBufferSize.Value;
            }

            var inner = new SystemSocket(transportSocket);
            var accessor = new ObservingSocketAccessor(inner);

            var source = new SingleReaderWriterConcurrentQueue<IMultiRefByteArray>(16);
            var pool = TestPools.GuardedRealPool();
            var consumer = new RealReceiveConsumer();
            var transport = new LowLevelTcpSocket(
                accessor, source, consumer.Handle, pool, policy, null, receiveBufferSize);

            return new RealSocketFixture(
                transportSocket, peer!, source, pool, consumer, transport, accessor);
        }

        /// <summary>Reads exactly <paramref name="count"/> bytes from the peer, bounded by <paramref name="timeout"/>.</summary>
        public static async Task<byte[]> ReadExactlyAsync(Socket socket, int count, TimeSpan timeout)
        {
            var result = new byte[count];
            int received = 0;
            while (received < count)
            {
                int n = await socket
                    .ReceiveAsync(result.AsMemory(received, count - received), SocketFlags.None)
                    .AsTask()
                    .WaitAsync(timeout)
                    .ConfigureAwait(false);
                if (n == 0)
                {
                    throw new EndOfStreamException(
                        $"Peer closed after {received} of {count} expected bytes.");
                }

                received += n;
            }

            return result;
        }

        /// <summary>Reads one bounded chunk (zero means EOF) from the peer.</summary>
        public static async Task<int> ReadSomeAsync(Socket socket, byte[] buffer, TimeSpan timeout)
            => await socket.ReceiveAsync(buffer.AsMemory(), SocketFlags.None)
                .AsTask()
                .WaitAsync(timeout)
                .ConfigureAwait(false);

        /// <summary>Deterministic xorshift byte pattern for byte-exact transfer assertions.</summary>
        public static byte[] BuildPattern(int seed, int length)
        {
            var data = new byte[length];
            uint state = unchecked((uint)seed * 2654435761u + 1u);
            for (int i = 0; i < length; i++)
            {
                state ^= state << 13;
                state ^= state >> 17;
                state ^= state << 5;
                data[i] = (byte)state;
            }

            return data;
        }

        public void Dispose()
        {
            try
            {
                Transport.Dispose();
            }
            finally
            {
                Peer.Dispose();
            }
        }
    }

    /// <summary>
    /// Observing decorator around <see cref="SystemSocket"/> that records whether the real accessor
    /// reported a pending (asynchronous) send/receive. Real backpressure must present as a pending
    /// native operation; it is never confused with a scripted <c>WouldBlock</c> return.
    /// </summary>
    internal sealed class ObservingSocketAccessor : ISocketAccessor
    {
        private readonly ISocketAccessor _inner;
        private int _pendingSendObservations;
        private int _pendingReceiveObservations;
        private int _sendPosts;
        private int _receivePosts;

        public ObservingSocketAccessor(ISocketAccessor inner)
        {
            _inner = inner;
        }

        public int SendPosts => Volatile.Read(ref _sendPosts);

        public int ReceivePosts => Volatile.Read(ref _receivePosts);

        /// <summary>Number of send posts that natively stayed pending (returned <c>true</c>).</summary>
        public int PendingSendObservations => Volatile.Read(ref _pendingSendObservations);

        /// <summary>Number of receive posts that natively stayed pending (returned <c>true</c>).</summary>
        public int PendingReceiveObservations => Volatile.Read(ref _pendingReceiveObservations);

        public SocketAsyncEventArgs CreateEventArgs() => _inner.CreateEventArgs();

        public bool SendAsync(SocketAsyncEventArgs e)
        {
            Interlocked.Increment(ref _sendPosts);
            bool pending = _inner.SendAsync(e);
            if (pending)
            {
                Interlocked.Increment(ref _pendingSendObservations);
            }

            return pending;
        }

        public bool ReceiveAsync(SocketAsyncEventArgs e)
        {
            Interlocked.Increment(ref _receivePosts);
            bool pending = _inner.ReceiveAsync(e);
            if (pending)
            {
                Interlocked.Increment(ref _pendingReceiveObservations);
            }

            return pending;
        }

        public void Shutdown(SocketShutdown how) => _inner.Shutdown(how);

        public void DisposeEventArgs(SocketAsyncEventArgs e) => _inner.DisposeEventArgs(e);

        public void Dispose() => _inner.Dispose();
    }

    /// <summary>
    /// A release-in-<c>finally</c> consumer that counts and records delivered bytes without ever
    /// retaining a borrowed buffer (the correctness sink is free to copy; it must never hide an
    /// ownership bug). Callback exceptions are captured, never thrown into the transport.
    /// </summary>
    internal sealed class RealReceiveConsumer
    {
        private readonly object _sync = new();
        private readonly List<byte> _bytes = new();
        private readonly List<Exception> _errors = new();
        private int _callCount;

        public int CallCount => Volatile.Read(ref _callCount);

        public Exception[] Errors
        {
            get { lock (_sync) { return _errors.ToArray(); } }
        }

        public byte[] Snapshot()
        {
            lock (_sync)
            {
                return _bytes.ToArray();
            }
        }

        public bool HasBytes(int count)
        {
            lock (_sync)
            {
                return _bytes.Count >= count;
            }
        }

        public bool WaitForBytes(int count, TimeSpan timeout)
            => TestWait.TrySpinUntil(() => HasBytes(count), timeout);

        public void Handle(IMultiRefByteArray buffer)
        {
            Interlocked.Increment(ref _callCount);
            try
            {
                lock (_sync)
                {
                    for (int i = 0; i < buffer.Count; i++)
                    {
                        _bytes.Add(buffer[i]);
                    }
                }
            }
            catch (Exception ex)
            {
                lock (_sync)
                {
                    _errors.Add(ex);
                }
            }
            finally
            {
                buffer.Release();
            }
        }
    }
}
