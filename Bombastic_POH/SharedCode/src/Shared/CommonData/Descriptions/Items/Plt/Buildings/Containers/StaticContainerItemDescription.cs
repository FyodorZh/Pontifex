using System;
using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class StaticContainerItemDescription : BaseContainerItemDescription
    {
        public override ItemType ItemDescType2
        {
            get { return Shared.CommonData.Plt.ItemType.StaticContainer; }
        }
    }
}
