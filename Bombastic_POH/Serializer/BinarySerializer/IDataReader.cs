using System.Collections.Generic;

namespace Serializer.BinarySerializer
{
    public interface IDataReader : IBinarySerializer, IBinaryReader
    {
        bool GetArray<T>(ref List<T> v);

        bool GetArray<T>(ref T[] v);
    }
}
