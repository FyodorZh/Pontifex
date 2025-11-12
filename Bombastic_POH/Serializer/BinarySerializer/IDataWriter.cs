namespace Serializer.BinarySerializer
{
    public interface IDataWriter : IBinarySerializer, IBinaryWriter
    {
        bool PrepareWriteArray(System.Collections.ICollection v);

        bool PrepareWriteArray(int count);
    }
}

