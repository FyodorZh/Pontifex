using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class ItemLevel : IDataStruct
    {
        [EditorField]
        private RpgParam[] _rpgParamsChange;

        [EditorField]
        private DropItems _dropItems;

        [EditorField]
        private int _time;

        [EditorField]
        private Price _price;

        [EditorField]
        private Requirement[] _requirements;

        [EditorField]
        private ItemLevelUnlock[] _unlocks;

        public ItemLevel()
        {
        }

        public ItemLevel(
            RpgParam[] rpgParamsChange,
            DropItems dropItems,
            int time,
            Price price,
            Requirement[] requirements,
            ItemLevelUnlock[] unlocks)
        {
            _rpgParamsChange = rpgParamsChange;
            _dropItems = dropItems;
            _time = time;
            _price = price;
            _requirements = requirements;
            _unlocks = unlocks;
        }

        public RpgParam[] RpgParamsChange
        {
            get { return _rpgParamsChange; }
        }

        public DropItems DropItems
        {
            get { return _dropItems; }
        }

        public System.TimeSpan Time
        {
            get { return System.TimeSpan.FromSeconds(_time); }
        }

        public Price Price
        {
            get { return _price; }
        }

        public Requirement[] Requirements
        {
            get { return _requirements; }
        }

        public ItemLevelUnlock[] Unlocks
        {
            get { return _unlocks; }
        }

        public virtual bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref _rpgParamsChange);
            dst.Add(ref _dropItems);
            dst.Add(ref _time);
            dst.Add(ref _price);
            dst.Add(ref _requirements);
            dst.Add(ref _unlocks);

            return true;
        }
    }
}
