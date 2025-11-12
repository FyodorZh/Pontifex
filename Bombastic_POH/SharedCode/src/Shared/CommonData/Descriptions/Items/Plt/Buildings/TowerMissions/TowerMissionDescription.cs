using Serializer.BinarySerializer;
using Serializer.Extensions;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class TowerMissionDescription : IDataStruct
    {
        public TowerMissionDescription()
        {
        }

        public TowerMissionDescription(string missionUid, int requiredPower, bool checkpoint, DropItems dropItems, DropItems oneTimeDropItems, DropItems dailyRewardDropItems, Price price, string mainImage, DropItems additionalRewardDropItems)
        {
            _missionUid = missionUid;
            _requiredPower = requiredPower;
            _checkpoint = checkpoint;
            _dropItems = dropItems;
            _oneTimeDropItems = oneTimeDropItems;
            _dailyRewardDropItems = dailyRewardDropItems;
            _price = price;
            _mainImage = mainImage;
            AdditionalRewardDropItems = additionalRewardDropItems;
        }

        [EditorField(EditorFieldParameter.MissionGuid)]
        private string _missionUid;

        [EditorField]
        private int _requiredPower;

        [EditorField]
        private bool _checkpoint;

        [EditorField]
        private DropItems _dropItems;
        
        [EditorField]
        private DropItems _oneTimeDropItems;
        
        [EditorField]
        private DropItems _dailyRewardDropItems;

        [EditorField]
        private Price _price;

        [EditorField(EditorFieldParameter.UnityTexture)]
        private string _mainImage;

        public string MissionUid
        {
            get { return _missionUid; }
        }

        public int RequiredPower
        {
            get { return _requiredPower; }
        }

        public bool Checkpoint
        {
            get { return _checkpoint; }
        }

        public DropItems DropItems
        {
            get { return _dropItems; }
        }

        public DropItems OneTimeDropItems
        {
            get { return _oneTimeDropItems; }
        }

        public DropItems DailyRewardDropItems
        {
            get { return _dailyRewardDropItems; }
        }

        [EditorField]
        public DropItems AdditionalRewardDropItems;

        public Price Price
        {
            get { return _price; }
        }

        public string MainImage
        {
            get { return _mainImage; }
        }

        public bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref _missionUid);
            dst.Add(ref _requiredPower);
            dst.Add(ref _checkpoint);
            dst.Add(ref _dropItems);
            dst.Add(ref _oneTimeDropItems);
            dst.Add(ref _dailyRewardDropItems);
            dst.Add(ref AdditionalRewardDropItems);
            dst.Add(ref _price);
            dst.Add(ref _mainImage);

            return true;
        }
    }
}
