using System;
using System.Collections.Generic;
using Actuarius.Collections;
using Pontifex.Utils;

namespace Pontifex.DeliveryManager
{
    internal class DeliveryReporter
    {
        private readonly List<DeliveryInfo> _confirmations = new List<DeliveryInfo>();

        public void Add(DeliveryInfo info)
        {
            _confirmations.Add(info);
        }

        public void Flush(DeliveryInfoSerializer deliveryInfoSerializer, int messageMaxByteSize, int safetyMargin, IConsumer<UnionDataList> dst)
        {
            int count = _confirmations.Count;
            if (count == 0)
                return;

            int packSize = (messageMaxByteSize - deliveryInfoSerializer.DeliveryInfoFixedOverhead - safetyMargin) / deliveryInfoSerializer.DeliveryInfoElementSize;

            int pos = 0;
            while (pos < count)
            {
                int len = Math.Min(packSize, count - pos);
                var infoMsg = deliveryInfoSerializer.CreateDeliveryReport(_confirmations, pos, len);
                infoMsg.PutFirst(new UnionData(false));
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
