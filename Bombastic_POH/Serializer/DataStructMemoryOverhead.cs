using Serializer.BinarySerializer;
using Serializer.Factory;

namespace Serializer
{
    public static class DataStructMemoryOverhead
    {
        public static int Calculate<TDataStruct>(TDataStruct emptyData) where TDataStruct : IDataStruct
        {
            ModelTinyFactory factory = new ModelTinyFactory(new[] { typeof(TDataStruct) });
            var writer = new DataWriter(factory);
            writer.Add(ref emptyData);
            return writer.Writer.ByteSize;
        }
    }
}
