using System.Text;
using Shared;

namespace Transport
{
    public static class AckUtils
    {
        public static byte[] AppendGuid(byte[] ackData, System.Guid guid)
        {
            byte[] bytes = guid.ToByteArray();
            return AppendPrefix(ackData, bytes);
        }

        public static ByteArraySegment GetGuid(ByteArraySegment ackData, out System.Guid guid)
        {
            ByteArraySegment guidBytes;
            ackData = GetPrefix(ackData, 16, out guidBytes);
            if (guidBytes.IsValid && guidBytes.Count == 16)
            {
                guid = new System.Guid(guidBytes.Clone());
            }
            else
            {
                guid = new System.Guid();
            }
            return ackData;
        }

        public static byte[] AckString(string ack)
        {
            return Encoding.UTF8.GetBytes(ack);
        }

        public static byte[] AppendPrefix(byte[] ackData, string prefix)
        {
            return AppendPrefix(ackData, Encoding.UTF8.GetBytes(prefix));
        }

        public static byte[] AppendPrefix(byte[] ackData, byte[] prefix)
        {
            byte[] res = new byte[prefix.Length + ackData.Length];

            System.Buffer.BlockCopy(prefix, 0, res, 0, prefix.Length);
            System.Buffer.BlockCopy(ackData, 0, res, prefix.Length, ackData.Length);

            return res;
        }

        public static ByteArraySegment GetPrefix(ByteArraySegment ackData, int length, out ByteArraySegment prefix)
        {
            if (ackData.IsValid && ackData.Count >= length)
            {
                prefix = ackData.Sub(0, length);
                return ackData.TrimLeft(length);
            }
            else
            {
                prefix = new ByteArraySegment();
                return new ByteArraySegment();
            }
        }

        public static ByteArraySegment CheckPrefix(ByteArraySegment ackData, string prefix)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(prefix);
            return CheckPrefix(ackData, bytes);
        }

        public static ByteArraySegment CheckPrefix(ByteArraySegment ackData, byte[] prefix)
        {
            if (prefix.Length > ackData.Count)
            {
                return new ByteArraySegment();
            }

            for (int i = 0; i < prefix.Length; ++i)
            {
                if (prefix[i] != ackData[i])
                {
                    return new ByteArraySegment();
                }
            }

            return ackData.TrimLeft(prefix.Length);
        }
    }
}