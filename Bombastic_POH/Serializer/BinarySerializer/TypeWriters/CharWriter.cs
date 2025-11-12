namespace Serializer.BinarySerializer.TypeWriters
{
    namespace TypeWriters
    {
        public interface ICharWriter
        {
            int Write(byte[] buffer, int startPos, char value);
        }

        class CharWriter_01 : ICharWriter
        {
            public int Write(byte[] buffer, int startPos, char value)
            {
                var unions = new ManagedUnions2 { charValue = value };
                buffer[startPos++] = unions.byte0;
                buffer[startPos++] = unions.byte1;
                return startPos;
            }
        }

        class CharWriter_10 : ICharWriter
        {
            public int Write(byte[] buffer, int startPos, char value)
            {
                var unions = new ManagedUnions2 { charValue = value };
                buffer[startPos++] = unions.byte1;
                buffer[startPos++] = unions.byte0;
                return startPos;
            }
        }
    }
}
