using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt.GameEvents
{
    public class GameEventCompensationDescription : IDataStruct
    {
        public GameEventCompensationDescription()
        {
        }

        public GameEventCompensationDescription(ItemWithCount whatToTake, ItemWithCount whatToGive, bool removeItem)
        {
            WhatToTake = whatToTake;
            WhatToGive = whatToGive;
            RemoveItem = removeItem;
        }

        [EditorField]
        public ItemWithCount WhatToTake;

        [EditorField]
        public ItemWithCount WhatToGive;

        [EditorField]
        public bool RemoveItem;

        public bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref WhatToTake);
            dst.Add(ref WhatToGive);
            dst.Add(ref RemoveItem);
            
            return true;
        }
    }
}