using System.Collections.Generic;
using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class DropItem : ItemWithCount
    {
        [EditorField]
        private DropItemParams[] _itemParams;

        public DropItem()
        {
        }

        public DropItem(short itemDescId, int count, DropItemParams[] itemParams)
            : base(itemDescId, count)
        {
            _itemParams = itemParams;
        }

        public DropItemParams[] ItemParams
        {
            get { return _itemParams; }
        }

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref _itemParams);

            return base.Serialize(dst);
        }
    }

    public static class DropItemExtensions
    {
        public static string AsString(this List<DropItem> dropItems)
        {
            var result = string.Empty;
         
            if (dropItems != null && dropItems.Count > 0)
            {
                for (int i = 0, c = dropItems.Count; i < c; ++i)
                {
                    var eachItem = dropItems[i];
                    if (!string.IsNullOrEmpty(result))
                    {
                        result += ", ";
                    }

                    result += "DescId=" + eachItem.ItemDescId.ToString() + " Count=" + eachItem.Count.ToString();

                    if (eachItem.ItemParams.Length > 0)
                    {
                        result += " Params=[" + eachItem.ItemParams.AsString() + "]";
                    }
                }
            }

            return result;
        }

        public static string AsString(this DropItemParams[] dropItemParams)
        {
            var result = string.Empty;
            
            if (dropItemParams != null && dropItemParams.Length > 0)
            {
                for (int i = 0, c = dropItemParams.Length; i < c; ++i)
                {
                    if (!string.IsNullOrEmpty(result))
                    {
                        result += ", ";
                    }

                    var eachParam = dropItemParams[i];

                    result += eachParam.Match(
                        t => "AutoOpen=" + t.AutoOpen.ToString(),
                        t => "Level=" + t.StartLevel.ToString(),
                        t => "Grade=" + t.StartGrade.ToString(),
                        t => "Offer=" + t.OfferTypeId.ToString());
                }
            }

            return result;
        }
    }
}
