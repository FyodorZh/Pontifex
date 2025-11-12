using Serializer.BinarySerializer;
using Serializer.Extensions.ID;

namespace Shared.NeoMeta.Items
{
    public class HeroEquipEquipmentRequest : IDataStruct
    {
        public ID<Item> HeroItemId;
        public ID<Item> EquipmentItemId;

        public HeroEquipEquipmentRequest()
        {
        }

        public HeroEquipEquipmentRequest(ID<Item> heroItemId, ID<Item> equipmentItemId)
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

        public enum ResultCode : byte
        {
            Ok = 0,
            ClassNotMatch = 1,
            IsBusy = 2
        }
    }
}

