using Actuarius.Memory;

namespace Pontifex.Utils
{
    public static class ZigZagVarIntSerializer
    {
        public static int GetIntEncodedSize(int value)
        {
            uint zigzag = (uint)((value << 1) ^ (value >> 31));
            int bytes = 0;
            do
            {
                zigzag >>= 7;
                bytes++;
            } while (zigzag > 0);
            return bytes;
        }

        public static bool WriteInt<TByteSink>(int value, ref TByteSink sink)
            where TByteSink : IByteSink
        {
            uint zigzag = (uint)((value << 1) ^ (value >> 31));

            while (zigzag > 0x7F)
            {
                if (!sink.Put((byte)((zigzag & 0x7F) | 0x80)))
                    return false;
                zigzag >>= 7;
            }
            return sink.Put((byte)zigzag);
        }

        public static bool WriteLong<TByteSink>(long value, ref TByteSink sink)
            where TByteSink : IByteSink
        {
            ulong zigzag = (ulong)((value << 1) ^ (value >> 63));

            while (zigzag > 0x7F)
            {
                if (!sink.Put((byte)((zigzag & 0x7F) | 0x80)))
                    return false;
                zigzag >>= 7;
            }
            return sink.Put((byte)zigzag);
        }

        public static bool ReadInt<TByteSource>(ref TByteSource source, out int value)
            where TByteSource : IByteSource
        {
            if (ReadLong(ref source, out var longVal) &&
                longVal >= int.MinValue && longVal <= int.MaxValue)
            {
                value = (int)longVal;
                return true;
            }
            value = 0;
            return false;
        }

        public static bool ReadLong<TByteSource>(ref TByteSource source, out long value)
            where TByteSource : IByteSource
        {
            ulong zigzag = 0;
            int shift = 0;

            for (int i = 0; i < 10; ++i)
            {
                if (!source.TryPop(out var b))
                {
                    value = 0;
                    return false;
                }

                zigzag |= (ulong)(b & 0x7F) << shift;
                shift += 7;

                if ((b & 0x80) == 0)
                {
                    value = (long)(zigzag >> 1) ^ (-(long)(zigzag & 1));
                    return true;
                }
            }

            value = 0;
            return false;
        }
    }
}
