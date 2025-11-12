namespace Serializer.BinarySerializer.TypeReaders
{
    namespace TypeReaders
    {
        public interface IFloatReader
        {
            float Read(byte[] buffer, ref int bufferPos);
        }

        class FloatReader_0123 : IFloatReader
        {
            public float Read(byte[] buffer, ref int bufferPos)
            {
                var unions = new ManagedUnions4 
                {
                    byte0 = buffer[bufferPos++],
                    byte1 = buffer[bufferPos++],
                    byte2 = buffer[bufferPos++],
                    byte3 = buffer[bufferPos++]
                };
                return unions.floatValue;
            }
        }

        class FloatReader_3210 : IFloatReader
        {
            public float Read(byte[] buffer, ref int bufferPos)
            {
                var unions = new ManagedUnions4
                {
                    byte3 = buffer[bufferPos++],
                    byte2 = buffer[bufferPos++],
                    byte1 = buffer[bufferPos++],
                    byte0 = buffer[bufferPos++]
                };
                return unions.floatValue;
            }
        }
    }
}
