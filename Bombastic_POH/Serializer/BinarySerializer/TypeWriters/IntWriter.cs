namespace Serializer.BinarySerializer.TypeWriters
{
    namespace TypeWriters
    {
        public interface IIntWriter
        {
            int Write(byte[] buffer, int startPos, int value);
        }

        class IntWriter_0123 : IIntWriter
        {
            public int Write(byte[] buffer, int startPos, int value)
            {
                var unions = new ManagedUnions4 { intValue = value };
                buffer[startPos++] = unions.byte0;
                buffer[startPos++] = unions.byte1;
                buffer[startPos++] = unions.byte2;
                buffer[startPos++] = unions.byte3;
                return startPos;
            }
        }

        class IntWriter_3210 : IIntWriter
        {
            public int Write(byte[] buffer, int startPos, int value)
            {
                var unions = new ManagedUnions4 { intValue = value };
                buffer[startPos++] = unions.byte3;
                buffer[startPos++] = unions.byte2;
                buffer[startPos++] = unions.byte1;
                buffer[startPos++] = unions.byte0;
                return startPos;
            }
        }
    }
}
