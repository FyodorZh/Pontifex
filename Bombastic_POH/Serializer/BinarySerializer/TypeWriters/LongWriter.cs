namespace Serializer.BinarySerializer.TypeWriters
{
    namespace TypeWriters
    {
        public interface ILongWriter
        {
            int Write(byte[] buffer, int startPos, long value);
        }

        class LongWriter_01234567 : ILongWriter
        {
            public int Write(byte[] buffer, int startPos, long value)
            {
                var unions = new ManagedUnions8 { longValue = value };
                buffer[startPos++] = unions.byte0;
                buffer[startPos++] = unions.byte1;
                buffer[startPos++] = unions.byte2;
                buffer[startPos++] = unions.byte3;
                buffer[startPos++] = unions.byte4;
                buffer[startPos++] = unions.byte5;
                buffer[startPos++] = unions.byte6;
                buffer[startPos++] = unions.byte7;
                return startPos;
            }
        }

        class LongWriter_76543210 : ILongWriter
        {
            public int Write(byte[] buffer, int startPos, long value)
            {
                var unions = new ManagedUnions8 { longValue = value };
                buffer[startPos++] = unions.byte7;
                buffer[startPos++] = unions.byte6;
                buffer[startPos++] = unions.byte5;
                buffer[startPos++] = unions.byte4;
                buffer[startPos++] = unions.byte3;
                buffer[startPos++] = unions.byte2;
                buffer[startPos++] = unions.byte1;
                buffer[startPos++] = unions.byte0;
                return startPos;
            }
        }
    }
}
