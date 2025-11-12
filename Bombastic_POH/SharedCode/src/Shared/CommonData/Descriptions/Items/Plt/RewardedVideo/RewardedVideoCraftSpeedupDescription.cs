using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class RewardedVideoCraftSpeedupDescription :
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
        public int MaxSpeedupsPerOrder;
        
        [EditorField]
        public int MaxSpeedupsSum;
        
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
            dst.Add(ref MaxSpeedupsPerOrder);
            dst.Add(ref MaxSpeedupsSum);

            return true;
        }
    }
}