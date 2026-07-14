using System;
using Pontifex.Utils;

namespace Pontifex.Transports.Direct.Delivery
{
    public sealed class PerfectDeliverySystem : IDeliverySystem
    {
        public event Action<UnionDataList>? Delivered;

        public void Deliver(UnionDataList message)
        {
            Delivered?.Invoke(message);
        }
    }
}
