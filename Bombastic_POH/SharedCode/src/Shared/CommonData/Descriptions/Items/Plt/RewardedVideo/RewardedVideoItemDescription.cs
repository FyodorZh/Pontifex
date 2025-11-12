using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class RewardedVideoItemDescription : ItemBaseDescription
    {
        public override ItemType ItemDescType2
        {
            get { return ItemType.RewardedVideo; }
        }

        [EditorField]
        public int ResetHour;

        [EditorField]
        public RewardedVideoBuildingSpeedupDescription BuildingSpeedups;

        [EditorField]
        public RewardedVideoCraftSpeedupDescription CraftSpeedups;

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref ResetHour);
            dst.Add(ref BuildingSpeedups);
            dst.Add(ref CraftSpeedups);

            return base.Serialize(dst);
        }
    }
}