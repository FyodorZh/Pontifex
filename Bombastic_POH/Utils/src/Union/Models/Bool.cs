using Serializer.BinarySerializer;

namespace Shared.Union
{
    public struct Bool : IDataStruct
    {
        private bool mValue;

        public bool Value
        {
            get { return mValue; }
            set { mValue = value; }
        }

        public Bool(bool value)
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