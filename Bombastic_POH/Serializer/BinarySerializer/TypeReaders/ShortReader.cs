namespace Serializer.BinarySerializer.TypeReaders
{
    namespace TypeReaders
    {
        public interface IShortReader
        {
            short Read(byte[] buffer, ref int bufferPos);
        }

        class ShortReader_01 : IShortReader
        {
            public short Read(byte[] buffer, ref int bufferPos)
            {
                var unions = new ManagedUnions2
                {
                    byte0 = buffer[bufferPos++],
                    byte1 = buffer[bufferPos++]
                };
                return unions.shortValue;
            }
        }

        class ShortReader_10 : IShortReader
        {
            public short Read(byte[] buffer, ref int bufferPos)
            {
                var unions = new ManagedUnions2
                {
                    byte1 = buffer[bufferPos++],
                    byte0 = buffer[bufferPos++]
                };
                return unions.shortValue;
            }
        }
    }
}
