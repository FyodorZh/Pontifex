using Serializer.BinarySerializer;

namespace Shared
{
    namespace CommonData
    {
        public interface IBaseUnitRuneItem
        {
            byte Id { get; }
            string Tag { get; }
            bool Enabled { get; }
        }

        public class UnitRuneItem : IBaseUnitRuneItem, IDataStruct
        {
            private byte mId;
            private string mTag;
            private bool mEnabled;

            public UnitRuneItem()
            {
            }

            public UnitRuneItem(string tag, bool enabled)
            {
                mTag = tag;
                mEnabled = enabled;
            }

            public byte Id { get { return mId; } }
            public string Tag { get { return mTag; } }
            public bool Enabled { get { return mEnabled; } }

            public void SetId(byte id) { mId = id; }

            public bool Serialize(IBinarySerializer dst)
            {
                dst.Add(ref mId);
                dst.Add(ref mTag);
                dst.Add(ref mEnabled);
                return true;
            }
        }
    }
}