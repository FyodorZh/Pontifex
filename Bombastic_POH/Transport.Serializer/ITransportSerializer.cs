using Shared;

namespace Transport.Serializer
{
    public interface ITransportSerializer<T>
    {
        byte[] Serialize(T data);
        T Deserialize(byte[] data);
        T Deserialize(ByteArraySegment data);
    }
}