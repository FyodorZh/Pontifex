using Serializer.BinarySerializer;
using Serializer.Extensions.ID;

namespace Shared.NeoMeta.Items
{
    public class HeroUnEquipEquipmentRequest : IDataStruct
    {
        public ID<Item> HeroItemId;
        public ID<Item> EquipmentItemId;

        public HeroUnEquipEquipmentRequest()
        {            
        }

        public HeroUnEquipEquipmentRequest(ID<Item> heroItemId, ID<Item> equipmentItemId)
        {
            HeroItemId = heroItemId;
            EquipmentItemId = equipmentItemId;
        }

        public bool Serialize(IBinarySerializer dst)
        {
            dst.AddId(ref HeroItemId);
            dst.AddId(ref EquipmentItemId);

            return true;
        }
    }
}
