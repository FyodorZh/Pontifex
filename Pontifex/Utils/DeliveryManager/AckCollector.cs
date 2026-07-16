using System;
using System.Collections.Generic;
using Actuarius.Collections;
using Pontifex.Utils;

namespace Pontifex.DeliveryManager
{
    internal class AckCollector
    {
        private readonly List<DeliveryInfo> _confirmations = new List<DeliveryInfo>();

        public void Add(DeliveryInfo info)
        {
            _confirmations.Add(info);
        }

        public void Flush(IWireMessageSerializer serializer, int messageMaxByteSize, int safetyMargin, IConsumer<UnionDataList> dst)
        {
            int count = _confirmations.Count;
            if (count == 0)
                return;

            int packSize = (messageMaxByteSize - serializer.DeliveryInfoFixedOverhead - safetyMargin) / serializer.DeliveryInfoElementSize;

            int pos = 0;
            while (pos < count)
            {
                int len = Math.Min(packSize, count - pos);
                var infoMsg = serializer.CreateDeliveryInfo(_confirmations, pos, len);
                dst.Put(infoMsg);
                pos += len;
            }

            _confirmations.Clear();
        }

        public void Clear()
        {
            _confirmations.Clear();
        }
    }
}
