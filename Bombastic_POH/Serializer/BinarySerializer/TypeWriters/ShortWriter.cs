namespace Serializer.BinarySerializer.TypeWriters
{
    namespace TypeWriters
    {
        public interface IShortWriter
        {
            int Write(byte[] buffer, int startPos, short value);
        }

        class ShortWriter_01 : IShortWriter
        {
            public int Write(byte[] buffer, int startPos, short value)
            {
                var unions = new ManagedUnions2 { shortValue = value };
                buffer[startPos++] = unions.byte0;
                buffer[startPos++] = unions.byte1;
                return startPos;
            }
        }

        class ShortWriter_10 : IShortWriter
        {
            public int Write(byte[] buffer, int startPos, short value)
            {
                var unions = new ManagedUnions2 { shortValue = value };
                buffer[startPos++] = unions.byte1;
                buffer[startPos++] = unions.byte0;
                return startPos;
            }
        }
    }
}
