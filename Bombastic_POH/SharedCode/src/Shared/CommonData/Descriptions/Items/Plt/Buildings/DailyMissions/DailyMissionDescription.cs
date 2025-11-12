using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt.DailyMissions
{
    public class DailyMissionDescription : DescriptionBase
    {
        [EditorField]
        private string _resourceMapName;

        [EditorField]
        public PlayerRequirement[] RollRequirements;

        public DailyMissionDescription()
        {
        }

        public DailyMissionDescription(string resourceMapName, PlayerRequirement[] rollRequirements)
        {
            _resourceMapName = resourceMapName;
            RollRequirements = rollRequirements;
        }     

        public string ResourceMapName
        {
            get { return _resourceMapName; }
        }
     
        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref _resourceMapName);
            dst.Add(ref RollRequirements);

            return base.Serialize(dst);
        }
    }
}
