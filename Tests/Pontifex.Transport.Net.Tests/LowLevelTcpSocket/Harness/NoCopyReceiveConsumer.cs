using Actuarius.Memory;

namespace Pontifex.Tests.NetSockets.LowLevelTcpSocketTests.Harness
{
    /// <summary>
    /// A receive consumer for allocation/load measurement that counts deliveries and bytes without
    /// copying or storing the payload. Unlike <see cref="ReceiveSink"/> it never allocates per
    /// delivery; ownership is explicit: the delivered buffer is released in <c>finally</c>, including
    /// when the optional validator throws. A caller can disable the release to prove the consumer
    /// does not release behind its back.
    /// </summary>
    internal sealed class NoCopyReceiveConsumer
    {
        private long _deliveredBytes;
        private int _deliveries;
        private int _releases;

        /// <summary>Optional validation run per delivery; an exception propagates after the release.</summary>
        public Action<IMultiRefByteArray>? Validate { get; set; }

        /// <summary>Release each delivered buffer in <c>finally</c>. Defaults to true (well-behaved consumer).</summary>
        public bool ReleaseBuffers { get; set; } = true;

        public int Deliveries => Volatile.Read(ref _deliveries);

        public long DeliveredBytes => Interlocked.Read(ref _deliveredBytes);

        public int ReleaseCount => Volatile.Read(ref _releases);

        public void Handle(IMultiRefByteArray buffer)
        {
            Interlocked.Increment(ref _deliveries);
            Interlocked.Add(ref _deliveredBytes, buffer.Count);
            try
            {
                Validate?.Invoke(buffer);
            }
            finally
            {
                if (ReleaseBuffers)
                {
                    buffer.Release();
                    Interlocked.Increment(ref _releases);
                }
            }
        }
    }
}
