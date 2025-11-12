using System;

namespace Shared.CommonData.Plt
{
    public static class PriceExtensions
    {
        public static int GetHardPrice(this Price price)
        {
            if (price == null || price.IsFree())
            {
                return 0;
            }

            int count = 0;
            for (int index = 0; index < price.Items.Length; ++index)
            {
                var eachPriceItem = price.Items[index];
                if (eachPriceItem.ItemDescId == ItemsConstants.ItemDescriptionId.Currency.Hard)
                {
                    count += eachPriceItem.Count;
                }
            }

            return count;
        }

        public static string AsString(this Price price)
        {
            if (price == null || price.IsFree())
            {
                return "Free";
            }

            string result = string.Empty;

            for (int index = 0; index < price.Items.Length; ++index)
            {
                if (!string.IsNullOrEmpty(result))
                {
                    result += "; ";
                }

                var eachItem = price.Items[index];
                result += "Id = " + eachItem.ItemDescId + ", Count = " + eachItem.Count;
            }

            return result;
        }

        public static Price Multiply(this Price price, int value)
        {
            var newItems = new ItemWithCount[price.Items.Length];
            for (int index = 0; index < price.Items.Length; ++index)
            {
                var eachItem = price.Items[index];

                newItems[index] = new ItemWithCount(eachItem.ItemDescId, eachItem.Count * value);
            }

            return new Price(newItems);
        }
    }
}
