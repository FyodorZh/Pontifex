namespace Serializer.BinarySerializer.TypeReaders
{
    namespace TypeReaders
    {
        public interface IIntReader
        {
            int Read(byte[] buffer, ref int bufferPos);
        }

        class IntReader_0123 : IIntReader
        {
            public int Read(byte[] buffer, ref int bufferPos)
            {
                ManagedUnions4 unions = new ManagedUnions4
                {
                    byte0 = buffer[bufferPos++],
                    byte1 = buffer[bufferPos++],
                    byte2 = buffer[bufferPos++],
                    byte3 = buffer[bufferPos++]
                };
                return unions.intValue;
            }
        }

        class IntReader_3210 : IIntReader
        {
            public int Read(byte[] buffer, ref int bufferPos)
            {
                ManagedUnions4 unions = new ManagedUnions4
                {
                    byte3 = buffer[bufferPos++],
                    byte2 = buffer[bufferPos++],
                    byte1 = buffer[bufferPos++],
                    byte0 = buffer[bufferPos++]
                };
                return unions.intValue;
            }
        }
    }
}
