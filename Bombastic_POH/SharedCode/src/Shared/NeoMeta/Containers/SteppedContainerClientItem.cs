using Serializer.BinarySerializer;
using Serializer.Extensions;
using Shared.CommonData.Plt;

namespace Shared.NeoMeta.Items
{
    public partial class SteppedContainerClientItem : Item
    {
        public SteppedContainerClientItem()
        {
        }

        public SteppedContainerClientItem(ID<Item> itemId, short descId, int step, int? nextUpdateTime, bool readyToOpen)
            : base(itemId, descId)
        {
            Step = step;
            NextUpdateTime = nextUpdateTime;
            ReadyToOpen = readyToOpen;
        }

        public override byte ItemDescType
        {
            get { return ItemType.SteppedContainerId; }
        }

        public int Step;

        public int? NextUpdateTime;

        public bool ReadyToOpen;

        public bool IsReadyToStep
        {
            get { return !ReadyToOpen && !NextUpdateTime.HasValue; }
        }

        public override bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref Step);
            dst.AddNullable(ref NextUpdateTime);
            dst.Add(ref ReadyToOpen);

            return base.Serialize(dst);
        }
    }
}