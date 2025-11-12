using System.Text;
using Serializer.BinarySerializer;
using Serializer.Extensions.ID;
using Shared.CommonData.Plt;
using Shared.NeoMeta.Items;

namespace Shared.NeoMeta.RewardedVideo
{
    public partial class RewardedVideoClientItem : Item
    {
        public RewardedVideoClientItem()
        {
        }

        public RewardedVideoClientItem(ID<Item> itemId, short descId/*, RewardedVideoClientBuildingSpeedups buildingSpeedups*/)
            : base(itemId, descId)
        {
//            BuildingSpeedups = buildingSpeedups;
        }

        public override byte ItemDescType
        {
            get { return ItemType.RewardedVideoId; }
        }

//        public RewardedVideoClientBuildingSpeedups BuildingSpeedups;

//        public override bool Serialize(IBinarySerializer dst)
//        {
//            dst.Add(ref BuildingSpeedups);
//
//            return base.Serialize(dst);
//        }

//        public override string ToString()
//        {
//            return string.Format("{0}, BuildingSpeedups: {1}", base.ToString(), BuildingSpeedups);
//        }
    }

//    public class RewardedVideoClientBuildingSpeedups : IDataStruct
//    {
//        public RewardedVideoClientBuildingSpeedups()
//        {
//        }
//
//        public RewardedVideoClientBuildingSpeedups(ID<Item>[] availableBuildingItemIds)
//        {
//            AvailableBuildingItemIds = availableBuildingItemIds;
//        }
//
//        public ID<Item>[] AvailableBuildingItemIds;
//
//        public bool Serialize(IBinarySerializer dst)
//        {
//            dst.AddId(ref AvailableBuildingItemIds);
//
//            return true;
//        }
//
//        public override string ToString()
//        {
//            var stringBuilder = new StringBuilder();
//            for (var i = 0; i < AvailableBuildingItemIds.Length; i++)
//            {
//                var id = AvailableBuildingItemIds[i];
//                stringBuilder.Append(id);
//                stringBuilder.Append(" ");
//            }
//            return string.Format("AvailableBuildingItemIds x{0}: [{1}]", AvailableBuildingItemIds.Length.ToString(), stringBuilder.ToString());
//        }
//    }
}