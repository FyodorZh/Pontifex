using Serializer.BinarySerializer;
using Serializer.Extensions;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt.StoryMissions
{
    public class StoryMission : IDataStruct
    {
        [EditorField(EditorFieldParameter.MissionGuid)]
        private string _missionUid;

        [EditorField]
        private int _requiredPower;

        [EditorField]
        private bool _sideMission;

        [EditorField]
        private Requirement[] _requirements;

        [EditorField]
        private Price _price;

        [EditorField]
        private StoryMissionDropItems _firstCompleteDropItems;

        [EditorField]
        private StoryMissionDropItems _secondCompleteDropItems;

        [EditorField, EditorLink("Items", "Items")]
        private short? _previewItemDescriptionId;

        public StoryMission()
        {
        }

        public StoryMission(
            string missionUid,
            int requiredPower,
            bool sideMission,
            Requirement[] requirements,
            Price price,
            StoryMissionDropItems firstCompleteDropItems,
            StoryMissionDropItems secondCompleteDropItems,
            short previewItemDescriptionId)
        {
            _missionUid = missionUid;
            _requiredPower = requiredPower;
            _sideMission = sideMission;
            _requirements = requirements;
            _price = price;
            _firstCompleteDropItems = firstCompleteDropItems;
            _secondCompleteDropItems = secondCompleteDropItems;
            _previewItemDescriptionId = previewItemDescriptionId;
        }

        public string MissionUid
        {
            get { return _missionUid; }
        }

        public int RequiredPower
        {
            get { return _requiredPower; }
        }

        public bool SideMission
        {
            get { return _sideMission; }
        }

        public Requirement[] Requirements
        {
            get { return _requirements; }
        }

        public Price Price
        {
            get { return _price; }
        }

        public StoryMissionDropItems FirstCompleteDropItems
        {
            get { return _firstCompleteDropItems; }
        }

        public StoryMissionDropItems SecondCompleteDropItems
        {
            get { return _secondCompleteDropItems; }
        }

        public short? PreviewItemDescriptionId
        {
            get { return _previewItemDescriptionId; }
        }

        public bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref _missionUid);
            dst.Add(ref _requiredPower);
            dst.Add(ref _sideMission);
            dst.Add(ref _requirements);
            dst.Add(ref _price);
            dst.Add(ref _firstCompleteDropItems);
            dst.Add(ref _secondCompleteDropItems);
            dst.AddNullable(ref _previewItemDescriptionId);

            return true;
        }
    }
}
