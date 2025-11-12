using Serializer.BinarySerializer;
using Serializer.Extensions.ID;

namespace Shared.NeoMeta.Items
{
    public class FreeHeroBuildingSlotRequest : IDataStruct
    {
        public ID<Item> BuildingId;
        public byte SlotIndex;

        public FreeHeroBuildingSlotRequest()
        {            
        }

        public FreeHeroBuildingSlotRequest(ID<Item> buildingId, byte slotIndex)
        {
            BuildingId = buildingId;
            SlotIndex = slotIndex;
        }

        public bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref SlotIndex);
            dst.AddId(ref BuildingId);

            return true;
        }

        public enum ResulCode : byte
        {
            Success,
            WaitingForCollect
        }
    }
}
