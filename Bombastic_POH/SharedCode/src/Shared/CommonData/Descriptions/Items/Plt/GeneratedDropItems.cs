using System.Collections.Generic;
using Serializer.BinarySerializer;

namespace Shared.CommonData.Plt
{
    public class GeneratedDropItems : IDataStruct
    {
        public GeneratedDropItems()
        {
        }

        public GeneratedDropItems(List<DropItem> dropItems, List<ActionDropItem> actionDropItems)
        {
            DropItems = dropItems;
            ActionDropItems = actionDropItems;
        }

        public List<DropItem> DropItems;

        public List<ActionDropItem> ActionDropItems;

        public bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref DropItems);
            dst.Add(ref ActionDropItems);

            return true;
        }
    }

    public static class GeneratedDropItemsExtensions
    {
        public static bool IsEmpty(this GeneratedDropItems dropItems)
        {
            return dropItems == null
                   || ((dropItems.ActionDropItems == null || dropItems.ActionDropItems.Count == 0)
                       && (dropItems.DropItems == null || dropItems.DropItems.Count == 0));
        }
    }
}