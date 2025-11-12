using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class ShipSlotDescription : IDataStruct
    {
        [EditorField, EditorLink("Items", "Items")]
        public short buildingDescriptionId;

        public virtual bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref buildingDescriptionId);

            return true;
        }
    }
}