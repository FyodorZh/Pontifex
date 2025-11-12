namespace Serializer.BinarySerializer
{
    public interface IBytesAllocator
    {
        byte[] Allocate(int length);
    }
}
