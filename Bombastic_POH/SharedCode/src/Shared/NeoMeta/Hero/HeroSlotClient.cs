using Serializer.BinarySerializer;
using Serializer.Extensions.ID;
using Shared.CommonData.Plt;
using Shared.NeoMeta.Items;

namespace Shared.NeoMeta
{
    public class HeroSlotClient : IDataStruct
    {
        public HeroSlotClient(ID<Item>? heroId, byte slotIndex, Requirement[] requirements)
        {
            HeroId = heroId;
            SlotIndex = slotIndex;
            Requirements = requirements;
        }

        public HeroSlotClient()
        {            
        }


        public ID<Item>? HeroId;
        public byte SlotIndex;
        public Requirement[] Requirements;

        public bool Serialize(IBinarySerializer dst)
        {
            dst.AddIdNullable(ref HeroId);
            dst.Add(ref SlotIndex);
            dst.Add(ref Requirements);

            return true;
        }

        public override string ToString()
        {
            return string.Format(
                "{{Type: '{0}', HeroId: {1}, SlotIndex: {2} Requirements: [{3}]}}",
                GetType().Name,
                HeroId.ToString(),
                SlotIndex.ToString(),
                Requirements != null ? string.Concat((object[])Requirements) : null);
        }
    }
}
