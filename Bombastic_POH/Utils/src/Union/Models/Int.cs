using Serializer.BinarySerializer;

namespace Shared.Union
{
    public struct Int : IDataStruct
    {
        private int mValue;

        public int Value
        {
            get { return mValue; }
            set { mValue = value; }
        }

        public Int(int value)
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