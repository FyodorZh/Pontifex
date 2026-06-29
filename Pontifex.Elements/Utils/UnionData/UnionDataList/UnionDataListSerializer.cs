using System;
using Actuarius.Memory;

namespace Pontifex.Elements
{
    public static class UnionDataListSerializer
    {
        private static void WriteCount<TByteSink>(int count, ref TByteSink sink)
            where TByteSink : IByteSink
        {
            if (count < 0)
                throw new ArgumentException("Count must be non-negative", nameof(count));

            int iterations = 0;

            while (count > 0x7F)
            {
                sink.Put((byte)((count & 0x7F) | 0x80));
                count >>= 7;
                if (iterations++ > 4)
                    throw new ArgumentException("Count is too large", nameof(count));
            }
            sink.Put((byte)count);
        }
        
        private static bool ReadCount<TByteSource>(ref TByteSource source, out int count)
            where TByteSource : IByteSource
        {
            int result = 0;
            int shift = 0;
            byte b = 0;

            for (int i = 0; i < 4; ++i)
            {
                if (!source.TryPop(out b))
                {
                    count = 0;
                    return false;
                }

                result |= (b & 0x7F) << shift;
                shift += 7;

                if ((b & 0x80) == 0)
                    break;
            }

            if ((b & 0x80) != 0)
            {
                count = 0;
                return false;
            }
            
            count = result;
            return true;
        }
    }
}