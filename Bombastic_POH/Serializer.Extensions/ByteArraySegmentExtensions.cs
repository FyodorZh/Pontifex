using Serializer.BinarySerializer;
using Shared;

namespace Serializer.Extensions
{
    public static class ByteArraySegmentExtensions
    {
        public static void AddByteArraySegment(this IBinarySerializer serializer, ref ByteArraySegment value)
        {
            if (serializer.isReader)
            {
                byte[] bytes = null;
                serializer.Add(ref bytes);
                value = new ByteArraySegment(bytes);
            }
            else
            {
                if (value.IsValid)
                {
                    var writer = (IDataWriter)serializer;
                    writer.PrepareWriteArray(value.Count);
                    writer.AddBytes(value.Array, value.Offset, value.Count);
                }
                else
                {
                    byte[] nullBytes = null;
                    serializer.Add(ref nullBytes);
                }
            }
        }
    }
}
