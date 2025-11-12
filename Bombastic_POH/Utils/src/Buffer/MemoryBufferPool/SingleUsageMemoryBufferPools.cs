namespace Shared.Buffer
{
    public sealed class SingleUsageMemoryBufferPool : IMemoryBufferPool
    {
        private readonly IConstructor<IMemoryBuffer> mCtor;
        private IMemoryBuffer mBuffer;

        public ILogger Log { get; set; }

        public event System.Action OnSingleUsageViolation;

        public SingleUsageMemoryBufferPool(IConstructor<IMemoryBuffer> ctor, ILogger log)
        {
            mCtor = ctor;
            mBuffer = ctor.Construct();
            Log = log;
        }

        IMemoryBuffer IPool<IMemoryBuffer>.Acquire()
        {
            IMemoryBuffer buffer = System.Threading.Interlocked.Exchange(ref mBuffer, null);
            if (buffer == null)
            {
                buffer = mCtor.Construct();
                OnFail();
            }
            return buffer;
        }

        void IPoolSink<IMemoryBuffer>.Release(IMemoryBuffer buffer)
        {
            if (buffer != null)
            {
                buffer.Clear();
                var oldBuffer = System.Threading.Interlocked.Exchange(ref mBuffer, buffer);
                if (oldBuffer != null)
                {
                    OnFail();
                    oldBuffer.Clear(); // and forget
                }
            }
        }

        private void OnFail()
        {
            Log.w("Invalid SingleUsageMemoryBufferPool usage");
            var evt = OnSingleUsageViolation;
            if (evt != null)
            {
                evt();
            }
        }
    }
}
