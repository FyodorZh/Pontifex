using Serializer.BinarySerializer;

namespace Shared.Union
{
    public struct Float : IDataStruct
    {
        private float mValue;

        public float Value
        {
            get { return mValue; }
            set { mValue = value; }
        }

        public Float(float value)
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
