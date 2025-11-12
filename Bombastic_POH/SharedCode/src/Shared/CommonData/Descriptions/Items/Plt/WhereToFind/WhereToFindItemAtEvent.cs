using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class WhereToFindItemAtEvent : WhereToFindItem
    {
        [EditorField, EditorLink("Items", "Items")]
        public short Event;

        public WhereToFindItemAtEvent()
            : this(WhereToFindItemTypes.Event)
        {
        }

        protected WhereToFindItemAtEvent(byte type)
            : base(type)
        {
        }

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref Event);

            return base.Serialize(dst);
        }
    }
}