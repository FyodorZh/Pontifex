using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class CurrencyItemDescription : ItemBaseDescription,
        ICanBeInPrice,
        IWithStages
    {
        private CurrencyStageDescription[] _stages;

        [EditorField] private short _startStageId;

        [EditorField] public WhereToFindItem[] BuyButtonLogic;
        
        [EditorField, EditorLink("Items", "Currency Stages")]
        private short[] StageIds;

        public override ItemType ItemDescType2
        {
            get { return Shared.CommonData.Plt.ItemType.Currency; }
        }


        public short StartStageId
        {
            get { return _startStageId; }
        }

        StageDescription[] IWithStages.Stages
        {
            get { return _stages; }
        }

        public CurrencyStageDescription[] Stages
        {
            get { return _stages; }
        }

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref BuyButtonLogic);
            dst.Add(ref StageIds);
            dst.Add(ref _startStageId);

            return base.Serialize(dst);
        }
        
        public override void OnPostprocess(ItemsDescriptions itemsDescriptions)
        {
            if (StageIds != null && StageIds.Length > 0)
            {
                _stages = new CurrencyStageDescription[StageIds.Length];
                for (int i = 0, cnt = StageIds.Length; i < cnt; i++)
                {
                    CurrencyStageDescription val;
                    if (itemsDescriptions.CurrencyStageDescriptions.TryGetValue(StageIds[i], out val))
                    {
                        _stages[i] = val;
                    }
                }
            }
        }
    }
}