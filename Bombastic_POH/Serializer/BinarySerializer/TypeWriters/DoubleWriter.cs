namespace Serializer.BinarySerializer.TypeWriters
{
    namespace TypeWriters
    {
        public interface IDoubleWriter
        {
            int Write(byte[] buffer, int startPos, double value);
        }

        class DoubleWriter_01234567 : IDoubleWriter
        {
            public int Write(byte[] buffer, int startPos, double value)
            {
                ManagedUnions8 unions = new ManagedUnions8 { doubleValue = value };
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

        class DoubleWriter_76543210 : IDoubleWriter
        {
            public int Write(byte[] buffer, int startPos, double value)
            {
                ManagedUnions8 unions = new ManagedUnions8 { doubleValue = value };
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
