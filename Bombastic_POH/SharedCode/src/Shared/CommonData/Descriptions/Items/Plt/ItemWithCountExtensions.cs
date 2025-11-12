using Shared.NeoMeta;

namespace Shared.CommonData.Plt
{
    public static class ItemWithCountExtensions
    {
        public static string AsString(this ItemWithCount[] items)
        {
            string result = string.Empty;

            for (int index = 0; index < items.Length; ++index)
            {
                if (!string.IsNullOrEmpty(result))
                {
                    result += ", ";
                }

                var eachItem = items[index];

                result += "[ItemDescId: " + eachItem.ItemDescId + ", Count: " + eachItem.Count + "]";
            }

            return result;
        }

        public static string AsString(this ItemIdWithCount[] items)
        {
            string result = string.Empty;

            for (int index = 0; index < items.Length; ++index)
            {
                if (!string.IsNullOrEmpty(result))
                {
                    result += ", ";
                }

                var eachItem = items[index];

                result += "[ItemId: " + eachItem.ItemId + ", Count: " + eachItem.Count + "]";
            }

            return result;
        }
    }
}
