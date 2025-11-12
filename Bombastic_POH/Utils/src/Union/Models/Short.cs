using Serializer.BinarySerializer;

namespace Shared.Union
{
    public struct Short : IDataStruct
    {
        private short mValue;

        public short Value
        {
            get { return mValue; }
            set { mValue = value; }
        }

        public Short(short value)
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