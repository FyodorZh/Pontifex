using Serializer.BinarySerializer;

namespace Serializer.Tools
{
    public struct DataRecord : IDataStruct
    {
        public int pos;
        public int size;

        public bool Serialize(IBinarySerializer stream)
        {
            stream.Add(ref pos);
            stream.Add(ref size);
            return true;
        }
    }
}