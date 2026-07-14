using System;
using Pontifex.Utils;

namespace Pontifex.VirtualDelivery
{
    public sealed class PerfectDeliverySystem : IDeliverySystem
    {
        private volatile bool _cleared;

        public event Action<UnionDataList>? Delivered;

        public void Deliver(UnionDataList message)
        {
            if (_cleared)
            {
                message.Release();
                return;
            }

            Delivered?.Invoke(message);
        }

        public void Clear()
        {
            _cleared = true;
        }
    }
}
