using Serializer.BinarySerializer;
using Shared;

namespace Serializer.Extensions.Pool
{
    public static class ByteArrayExtension
    {
        public static void AddByteArray(
            this IBinarySerializer serializer,
            ref ByteArray inputData)
        {
            if (serializer.isReader)
            {
                if (inputData != null)
                {
                    inputData.Release();
                }

                byte[] bytes = null;
                serializer.Add(ref bytes);

                inputData = ByteArray.AssumeControl(bytes, bytes.Length, true);
            }
            else
            {
                var writer = (IDataWriter)serializer;
                if (!writer.PrepareWriteArray(inputData.Length))
                {
                    return;
                }

                writer.AddBytes(inputData.Data, 0, inputData.Length);
            }
        }
    }
}
