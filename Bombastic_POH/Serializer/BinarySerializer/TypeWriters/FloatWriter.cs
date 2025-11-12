namespace Serializer.BinarySerializer.TypeWriters
{
    namespace TypeWriters
    {
        public interface IFloatWriter
        {
            int Write(byte[] buffer, int startPos, float value);
        }

        class FloatWriter_0123 : IFloatWriter
        {
            public int Write(byte[] buffer, int startPos, float value)
            {
                var unions = new ManagedUnions4 { floatValue = value };
                buffer[startPos++] = unions.byte0;
                buffer[startPos++] = unions.byte1;
                buffer[startPos++] = unions.byte2;
                buffer[startPos++] = unions.byte3;
                return startPos;
            }
        }

        class FloatWriter_3210 : IFloatWriter
        {
            public int Write(byte[] buffer, int startPos, float value)
            {
                var unions = new ManagedUnions4 { floatValue = value };
                buffer[startPos++] = unions.byte3;
                buffer[startPos++] = unions.byte2;
                buffer[startPos++] = unions.byte1;
                buffer[startPos++] = unions.byte0;
                return startPos;
            }
        }
    }
}
