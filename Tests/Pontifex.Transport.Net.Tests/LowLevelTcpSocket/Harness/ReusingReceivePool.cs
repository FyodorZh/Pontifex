using Actuarius.Memory;

namespace Pontifex.Tests.NetSockets.LowLevelTcpSocketTests.Harness
{
    /// <summary>
    /// A receive-owner pool for allocation measurement: it hands back a fixed set of preallocated
    /// <see cref="MultiRefByteArray"/> owners and keeps one reference alive across reuses, so the
    /// measuring window isolates transport/view behavior instead of per-acquire owner allocation.
    /// Arbitrary injected production pools may still allocate an owner wrapper per acquisition; that
    /// cost is reported separately and is out of this stage's budget.
    /// </summary>
    internal sealed class ReusingReceivePool : IConcurrentPool<IMultiRefByteArray, int>
    {
        private readonly object _sync = new();
        private readonly MultiRefByteArray[] _buffers;
        private int _next;

        public ReusingReceivePool(int bufferSize, int bufferCount)
        {
            _buffers = new MultiRefByteArray[bufferCount];
            for (int i = 0; i < bufferCount; i++)
            {
                _buffers[i] = new MultiRefByteArray(new byte[bufferSize]);
            }
        }

        public IMultiRefByteArray Acquire(int param0)
        {
            MultiRefByteArray buffer;
            lock (_sync)
            {
                buffer = _buffers[_next++ % _buffers.Length];
            }

            // Keep one reference alive so the transport's exactly-once release never reaches zero
            // and the same preallocated owner can be handed out again.
            buffer.AddRef();
            return buffer;
        }

        public (IMultiRefByteArray resource, IPoolSink<IMultiRefByteArray> poolSink) AcquireEx(int param0)
            => (Acquire(param0), this);

        public void Release(IMultiRefByteArray? resource)
        {
            // Never reached: the retained reference keeps each owner alive.
        }
    }
}
