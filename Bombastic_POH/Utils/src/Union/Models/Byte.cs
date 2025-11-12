using Serializer.BinarySerializer;

namespace Shared.Union
{
    public struct Byte : IDataStruct
    {
        private byte mValue;

        public byte Value
        {
            get { return mValue; }
            set { mValue = value; }
        }

        public Byte(byte value)
        {
            mValue = value;
        }

        public bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref mValue);
            return true;
        }
    }
}