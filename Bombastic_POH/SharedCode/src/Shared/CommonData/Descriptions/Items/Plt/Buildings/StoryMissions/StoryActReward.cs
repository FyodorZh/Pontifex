using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt.StoryMissions
{
    public class StoryActReward : IDataStruct
    {
        public StoryActReward()
        {            
        }

        public StoryActReward(byte rewardId, short minStarsCount, DropItems dropItems)
        {
            RewardId = rewardId;
            MinStarsCount = minStarsCount;
            DropItems = dropItems;
        }        

        [EditorField]
        public byte RewardId;

        [EditorField]
        public short MinStarsCount;

        [EditorField]
        public DropItems DropItems;

        [EditorField(EditorFieldParameter.UnityTexture)]
        public string Icon;

        public bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref RewardId);
            dst.Add(ref MinStarsCount);
            dst.Add(ref DropItems);
            dst.Add(ref Icon);

            return true;
        }
    }
}