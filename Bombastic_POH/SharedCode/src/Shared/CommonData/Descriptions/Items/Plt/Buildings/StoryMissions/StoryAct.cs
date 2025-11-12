using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt.StoryMissions
{
    public class StoryAct : IDataStruct
    {
        public StoryAct()
        {            
        }

        public StoryAct(string actUid, Requirement[] requirements, StoryActReward[] rewards)
        {
            _actUid = actUid;
            _requirements = requirements;
            _rewards = rewards;
        }        

        [EditorField(EditorFieldParameter.ActGuid)]
        private string _actUid;

        [EditorField]
        private Requirement[] _requirements;
        
        [EditorField]
        private StoryActReward[] _rewards;

        public string ActUid
        {
            get { return _actUid; }
        }

        public Requirement[] Requirements
        {
            get { return _requirements; }
        }

        public StoryActReward[] Rewards
        {
            get { return _rewards; }
        }

        public bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref _actUid);
            dst.Add(ref _requirements);
            dst.Add(ref _rewards);

            return true;
        }
    }
}
