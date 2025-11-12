using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt.StoryMissions
{
    public class StoryMissionsBuildingItemDescription : DefaultBuildingItemDescription
    {
        [EditorField, EditorLink("Items", "Story Missions Building Data")]
        private short _storyMissionData;

        private StoryAct[] _acts;
        private StoryMission[] _missions;

        public StoryMissionsBuildingItemDescription()
        {
        }

        public StoryMissionsBuildingItemDescription(
            string name,
            string text,
            short position,
            BuildingItemLevel[] grades,
            short startGrade,
            string buttonText,
            StoryAct[] acts,
            StoryMission[] missions)
            : base(name, text, position, grades, startGrade, buttonText)
        {
            _acts = acts;
            _missions = missions;
        }

        public override ItemType ItemDescType2
        {
            get { return ItemType.StoryMissionsBuilding; }
        }

        public StoryMission[] Missions
        {
            get { return _missions; }
        }

        public StoryAct[] Acts
        {
            get { return _acts; }
        }

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref _storyMissionData);
            return base.Serialize(dst);
        }

        public override void OnPostprocess(ItemsDescriptions itemsDescriptions)
        {
            StoryMissionsBuildingDataDescription desc;
            if (itemsDescriptions.StoryMissionsBuildingDataDescription.TryGetValue(_storyMissionData, out desc))
            {
                _acts = desc.acts;
                _missions = desc.missions;
            }
        }
    }
}
