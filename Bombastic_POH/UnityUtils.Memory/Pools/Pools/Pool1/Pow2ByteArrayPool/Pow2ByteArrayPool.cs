using Actuarius;

namespace Shared.Pooling
{
    public class Pow2ByteArrayPool : Pool<byte[], int>
    {
        private class SubPoolCtor : IConstructor<IPool<byte[]>, int>
        {
            public IPool<byte[]> Construct(int pow2Length)
            {
                return new Pool<byte[]>(new ArrayCtor(pow2Length));
            }
        }

        public Pow2ByteArrayPool()
            : base(new SubPoolCtor(), new TrivialDictionary<int, IPool<byte[]>>())
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