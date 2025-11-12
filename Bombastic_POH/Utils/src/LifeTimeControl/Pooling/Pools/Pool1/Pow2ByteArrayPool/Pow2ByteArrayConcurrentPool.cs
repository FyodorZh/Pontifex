namespace Shared.Pooling
{
    public class Pow2ByteArrayConcurrentPool : ConcurrentPool<byte[], int>
    {
        private class SubPoolCtor : IConstructor<IConcurrentPool<byte[]>, int>
        {
            private readonly int mCapacity;

            public SubPoolCtor(int capacity)
            {
                mCapacity = capacity;
            }

            public IConcurrentPool<byte[]> Construct(int pow2Length)
            {
                return new ConcurrentPool<byte[]>(new ArrayCtor(pow2Length), mCapacity);
            }
        }

        public Pow2ByteArrayConcurrentPool(int capacity)
            : base(new SubPoolCtor(capacity), new TrivialConcurrentDictionary<int, IPool<byte[]>>())
        {
        }

        protected sealed override int Classify(int requiredArrayLength)
        {
            return BitMath.NextPow2((uint)requiredArrayLength);
        }

        protected sealed override int Classify(byte[] array)
        {
            return array.Length;
        }
    }
}