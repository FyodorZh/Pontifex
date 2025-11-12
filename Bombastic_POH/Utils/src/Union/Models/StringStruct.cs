using Serializer.BinarySerializer;
using System;
using System.Text;

namespace Shared.Union
{
    public struct StringStruct<TBytes> : IDataStruct
        where TBytes : struct, IArray<byte>, IDataStruct
    {
        private TBytes mData;

        [ThreadStatic]
        private static byte[] mBytes;
        private static byte[] Bytes
        {
            get
            {
                byte[] bytes = mBytes;
                if (bytes == null)
                {
                    bytes = new byte[default(TBytes).Capacity];
                    mBytes = bytes;
                }
                return bytes;
            }
        }

        public StringStruct(string str)
            : this()
        {
            if (str == null)
            {
                mData.SetNull();
            }
            else
            {
                byte[] bytes = Bytes;

                int len = Encoding.UTF8.GetBytes(str, 0, str.Length, bytes, 0);
                mData = Array.CopyOf<TBytes, byte>(bytes, 0, len);
            }
        }

        public override string ToString()
        {
            byte[] bytes = Bytes;

            if (mData.IsNull)
            {
                return "null";
            }

            mData.CopyTo(bytes);

            return Encoding.UTF8.GetString(bytes, 0, mData.Length);
        }

        public bool Serialize(IBinarySerializer dst)
        {
            return mData.Serialize(dst);
        }
    }
}
