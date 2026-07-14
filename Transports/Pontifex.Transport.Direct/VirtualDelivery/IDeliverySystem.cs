using System;
using Pontifex.Utils;

namespace Pontifex.VirtualDelivery
{
    public interface IDeliverySystem
    {
        event Action<UnionDataList>? Delivered;
        void Deliver(UnionDataList message);
        void Clear();
    }
}
