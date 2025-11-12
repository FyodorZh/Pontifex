namespace Serializer.BinarySerializer
{
    public interface IDataStruct
    {
        bool Serialize(IBinarySerializer dst);
    }
}
