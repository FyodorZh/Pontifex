using System.Collections.Generic;
using Actuarius.Collections;
using Actuarius.Memory;
using Pontifex.Utils;

namespace Pontifex.Delivery
{
    internal class DeliveryInfoSerializer
    {
        private readonly ICollectablePool _pool;
        private readonly List<DeliveryInfo> _currentReport = new();

        public DeliveryInfoSerializer(ICollectablePool pool)
        {
            _pool = pool;
        }

        public int DeliveryInfoFixedOverhead => 4;
        public int DeliveryInfoElementSize => 5;

        public IReadOnlyList<DeliveryInfo> CurrentDeliveryReport => _currentReport;

        public UnionDataList CreateDeliveryReport(IReadOnlyList<DeliveryInfo> confirmations, int start, int count)
        {
            var msg = _pool.Acquire<UnionDataList>();
            msg.PutLast(new UnionData((ushort)count));

            for (int i = start; i < start + count; ++i)
            {
                msg.PutLast(new UnionData(confirmations[i].Id.Id));
                msg.PutLast(new UnionData(confirmations[i].ChunkId));
            }

            return msg;
        }

        public bool LoadDeliveryReport(UnionDataList data)
        {
            _currentReport.Clear();

            if (!data.TryPopFirst(out ushort count))
                return false;

            for (int i = 0; i < count; ++i)
            {
                if (!data.TryPopFirst(out ushort id) || !data.TryPopFirst(out byte chunkId))
                    return false;

                _currentReport.Add(new DeliveryInfo(new DeliveryId(id), chunkId));
            }

            return true;
        }
    }
}
