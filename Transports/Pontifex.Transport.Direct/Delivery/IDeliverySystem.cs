using System;
using Pontifex.Utils;

namespace Pontifex.Transports.Direct.Delivery
{
    public interface IDeliverySystem
    {
        event Action<UnionDataList>? Delivered;
        void Deliver(UnionDataList message);
    }
}
