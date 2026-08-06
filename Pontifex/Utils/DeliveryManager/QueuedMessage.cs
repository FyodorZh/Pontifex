using Pontifex.Utils;

namespace Pontifex.Delivery
{
    internal readonly struct QueuedMessage
    {
        public readonly DeliveryInfo Info;
        public readonly IReadOnlyUnionDataList Data;

        public QueuedMessage(DeliveryInfo info, UnionDataList data)
        {
            Info = info;
            Data = data;
        }
    }
}
