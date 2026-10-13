using Actuarius.Memory;

namespace Pontifex.Tests.NetSockets.LowLevelTcpSocketTests.Harness
{
    /// <summary>
    /// Records buffers delivered through the socket's <c>onReceived</c> callback and models the
    /// ownership decisions a real consumer makes (release, release-then-throw, throw-before-release).
    /// </summary>
    internal sealed class ReceiveSink
    {
        private readonly object _sync = new();
        private readonly List<byte[]> _received = new();
        private int _callCount;

        /// <summary>Release the delivered buffer on the normal path. Defaults to true (a well-behaved consumer).</summary>
        public bool ReleaseBuffers { get; set; } = true;

        /// <summary>When a throw is configured, release the buffer before throwing (models a consumer that owned it).</summary>
        public bool ReleaseBeforeThrow { get; set; }

        /// <summary>When set, the callback throws, exercising the transport's error path.</summary>
        public Exception? ThrowOnReceive { get; set; }

        /// <summary>Runs after recording but before any release/throw; useful for blocking or inspection.</summary>
        public Action<IMultiRefByteArray>? OnReceived { get; set; }

        public int CallCount => Volatile.Read(ref _callCount);

        public byte[][] Received
        {
            get { lock (_sync) { return _received.ToArray(); } }
        }

        public byte[] Last
        {
            get
            {
                lock (_sync)
                {
                    return _received.Count == 0 ? Array.Empty<byte>() : _received[^1];
                }
            }
        }

        public void Handle(IMultiRefByteArray buffer)
        {
            Interlocked.Increment(ref _callCount);

            byte[] copy = Copy(buffer);
            lock (_sync)
            {
                _received.Add(copy);
            }

            OnReceived?.Invoke(buffer);

            if (ThrowOnReceive != null)
            {
                if (ReleaseBeforeThrow)
                {
                    buffer.Release();
                }

                throw ThrowOnReceive;
            }

            if (ReleaseBuffers)
            {
                buffer.Release();
            }
        }

        private static byte[] Copy(IMultiRefByteArray buffer)
        {
            var copy = new byte[buffer.Count];
            if (buffer.Count > 0)
            {
                buffer.CopyTo(copy, 0, 0, buffer.Count);
            }

            return copy;
        }
    }
}
