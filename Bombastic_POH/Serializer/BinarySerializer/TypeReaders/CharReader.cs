namespace Serializer.BinarySerializer.TypeReaders
{
    namespace TypeReaders
    {
        public interface ICharReader
        {
            char Read(byte[] buffer, ref int bufferPos);
        }

        class CharReader_01 : ICharReader
        {
            public char Read(byte[] buffer, ref int bufferPos)
            {
                var unions = new ManagedUnions2
                {
                    byte0 = buffer[bufferPos++],
                    byte1 = buffer[bufferPos++]
                };
                return unions.charValue;
            }
        }

        class CharReader_10 : ICharReader
        {
            public char Read(byte[] buffer, ref int bufferPos)
            {
                var unions = new ManagedUnions2
                {
                    byte1 = buffer[bufferPos++],
                    byte0 = buffer[bufferPos++]
                };
                return unions.charValue;
            }
        }
    }
}
