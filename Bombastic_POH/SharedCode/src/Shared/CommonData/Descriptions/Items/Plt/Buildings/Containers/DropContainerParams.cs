using Serializer.BinarySerializer;
using Shared.CommonData.Attributes;

namespace Shared.CommonData.Plt
{
    public class DropContainerParams : DropItemParams
    {
        public DropContainerParams()
        {
        }

        public DropContainerParams(bool autoOpen)
        {
            AutoOpen = autoOpen;
        }

        [EditorField]
        public bool AutoOpen;

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref AutoOpen);

            return true;
        }
    }
}
