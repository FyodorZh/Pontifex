namespace Shared.Pooling
{
    internal class ArrayCtor : IConstructor<byte[]>
    {
        private readonly int mSize;

        public ArrayCtor(int size)
        {
            mSize = size;
        }

        public byte[] Construct()
        {
            return new byte[mSize];
        }
    }
}