namespace Serializer
{
    public interface IAllocator
    {
        byte[] Allocate(int size);
        byte[] Reallocate(byte[] bytes, int newSize);

        void Deallocate(byte[] bytes);
    }

    public struct DefaultAllocator : IAllocator
    {
        byte[] IAllocator.Allocate(int size)
        {
            return new byte[size];
        }

        public byte[] Reallocate(byte[] bytes, int newSize)
        {
            byte[] newArray = new byte[newSize];
            System.Buffer.BlockCopy(bytes, 0, newArray, 0, bytes.Length);
            return newArray;
        }

        public void Deallocate(byte[] bytes)
        {
            // DO NOTHING
        }
    }
}
