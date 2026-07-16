using System;
using System.Collections.Generic;
using Actuarius.Collections;
using Actuarius.Memory;
using Pontifex.Utils;

namespace Pontifex.DeliveryManager
{
    internal class DeliveryRporter
    {
        private const byte TypeDeliveryReport = 2;

        private readonly ICollectablePool _pool;
        private readonly List<DeliveryInfo> _confirmations = new List<DeliveryInfo>();

        public DeliveryRporter(ICollectablePool pool)
        {
            _pool = pool;
        }

        public int DeliveryInfoFixedOverhead => 6;
        public int DeliveryInfoElementSize => 5;

        // ── Accumulation ──

        public void Add(DeliveryInfo info)
        {
            _confirmations.Add(info);
        }

        // ── Serialization + flush ──

        public void FlushDeliveryReports(int messageMaxByteSize, int safetyMargin, IConsumer<UnionDataList> dst)
        {
            int count = _confirmations.Count;
            if (count == 0)
                return;

            int packSize = (messageMaxByteSize - DeliveryInfoFixedOverhead - safetyMargin) / DeliveryInfoElementSize;

            int pos = 0;
            while (pos < count)
            {
                int len = Math.Min(packSize, count - pos);
                var infoMsg = CreateDeliveryReport(_confirmations, pos, len);
                dst.Put(infoMsg);
                pos += len;
            }

            _confirmations.Clear();
        }

        private UnionDataList CreateDeliveryReport(IReadOnlyList<DeliveryInfo> confirmations, int start, int count)
        {
            var msg = _pool.Acquire<UnionDataList>();
            msg.PutLast(new UnionData(TypeDeliveryReport));
            msg.PutLast(new UnionData((ushort)count));

            for (int i = start; i < start + count; ++i)
            {
                msg.PutLast(new UnionData(confirmations[i].Id.Id));
                msg.PutLast(new UnionData(confirmations[i].ChunkId));
            }

            return msg;
        }

        // ── Parsing (receive direction) ──

        public bool ParseDeliveryReport(UnionDataList data, List<DeliveryInfo> confirmations)
        {
            if (!data.TryPopFirst(out byte type) || type != TypeDeliveryReport)
                return false;

            if (!data.TryPopFirst(out ushort count))
                return false;

            for (int i = 0; i < count; ++i)
            {
                if (!data.TryPopFirst(out ushort id) || !data.TryPopFirst(out byte chunkId))
                    return false;

                confirmations.Add(new DeliveryInfo(new DeliveryId(id), chunkId));
            }

            return true;
        }

        public void Clear()
        {
            _confirmations.Clear();
        }
    }
}
