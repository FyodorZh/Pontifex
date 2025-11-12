using Serializer.BinarySerializer;

namespace Shared.NeoMeta.StoryMissions
{
    public class StoryAct : IDataStruct
    {
        public StoryAct()
        {
        }

        public StoryAct(string actUid, byte[] collectedRewardsIds)
        {
            ActUid = actUid;
            CollectedRewardsIds = collectedRewardsIds;
        }

        public string ActUid;
        public byte[] CollectedRewardsIds;

        public virtual bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref ActUid);
            dst.Add(ref CollectedRewardsIds);

            return true;
        }
    }
}
