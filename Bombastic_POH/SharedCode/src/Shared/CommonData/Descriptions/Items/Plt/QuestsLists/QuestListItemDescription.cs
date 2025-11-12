using Serializer.BinarySerializer;
using Serializer.Extensions;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class QuestListItemDescription : ItemBaseDescription
    {
        public QuestListItemDescription()
        {
        }

        public QuestListItemDescription(
            short id,
            string tag)
        {
            Id = id;
            Tag = tag;
        }
        
        [EditorField, EditorLink("Items", "Quest List Item Data")]
        public short _data;

        public override ItemType ItemDescType2
        {
            get { return ItemType.QuestsList; }
        }
        

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref _data);
            return base.Serialize(dst);
        }

    }
}