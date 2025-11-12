using Serializer.BinarySerializer;

namespace Serializer.Tools
{
    public struct DataTypeDesc : IDataStruct
    {
        public int DataTypeId;
        public int DataCount;
        public int DataSize;
        public int StartDataPos;
        public DataRecord[] refDataPos;

        public bool Serialize(IBinarySerializer stream)
        {
            stream.Add(ref DataTypeId);
            stream.Add(ref DataCount);
            stream.Add(ref DataSize);
            stream.Add(ref StartDataPos);
            stream.Add(ref refDataPos);
            return true;
        }
    }
}