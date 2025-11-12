namespace Serializer.BinarySerializer.TypeReaders
{
    namespace TypeReaders
    {
        public interface ILongReader
        {
            long Read(byte[] buffer, ref int bufferPos);
        }

        class LongReader_01234567 : ILongReader
        {
            public long Read(byte[] buffer, ref int bufferPos)
            {
                var unions = new ManagedUnions8
                {
                    byte0 = buffer[bufferPos++],
                    byte1 = buffer[bufferPos++],
                    byte2 = buffer[bufferPos++],
                    byte3 = buffer[bufferPos++],
                    byte4 = buffer[bufferPos++],
                    byte5 = buffer[bufferPos++],
                    byte6 = buffer[bufferPos++],
                    byte7 = buffer[bufferPos++]
                };
                return unions.longValue;
            }
        }

        class LongReader_76543210 : ILongReader
        {
            public long Read(byte[] buffer, ref int bufferPos)
            {
                var unions = new ManagedUnions8
                {
                    byte7 = buffer[bufferPos++],
                    byte6 = buffer[bufferPos++],
                    byte5 = buffer[bufferPos++],
                    byte4 = buffer[bufferPos++],
                    byte3 = buffer[bufferPos++],
                    byte2 = buffer[bufferPos++],
                    byte1 = buffer[bufferPos++],
                    byte0 = buffer[bufferPos++]
                };
                return unions.longValue;
            }
        }
    }
}
