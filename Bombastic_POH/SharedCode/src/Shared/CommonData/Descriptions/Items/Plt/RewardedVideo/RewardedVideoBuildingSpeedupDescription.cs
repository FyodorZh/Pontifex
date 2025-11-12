using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class RewardedVideoBuildingSpeedupDescription :
        IDataStruct,
        IRewardedVideoSpeedupDescription
    {
        [EditorField]
        public int SpeedupPercent;

        [EditorField]
        public int SpeedupStepMinutes;
        
        [EditorField]
        public int MinSpeedupMinutes;

        [EditorField]
        public int MaxSpeedupMinutes;

        [EditorField]
        public int MaxSpeedupsPerBuilding;

        [EditorField]
        public int MaxSpeedupsSum;

        [EditorField]
        public ItemRequirement[] BuildingRequirements;

        int IRewardedVideoSpeedupDescription.SpeedupPercent
        {
            get { return SpeedupPercent; }
        }

        System.TimeSpan IRewardedVideoSpeedupDescription.SpeedupStep
        {
            get { return System.TimeSpan.FromMinutes(SpeedupStepMinutes); }
        }

        System.TimeSpan IRewardedVideoSpeedupDescription.MaxSpeedupTime
        {
            get { return System.TimeSpan.FromMinutes(MaxSpeedupMinutes); }
        }

        System.TimeSpan IRewardedVideoSpeedupDescription.MinSpeedupTime
        {
            get { return System.TimeSpan.FromMinutes(MinSpeedupMinutes); }
        }

        public bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref SpeedupPercent);
            dst.Add(ref SpeedupStepMinutes);
            dst.Add(ref MinSpeedupMinutes);
            dst.Add(ref MaxSpeedupMinutes);
            dst.Add(ref MaxSpeedupsPerBuilding);
            dst.Add(ref MaxSpeedupsSum);
            dst.Add(ref BuildingRequirements);

            return true;
        }
    }
}