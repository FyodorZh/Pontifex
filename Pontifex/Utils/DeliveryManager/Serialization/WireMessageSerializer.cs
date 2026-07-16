using System.Collections.Generic;
using Actuarius.Memory;
using Pontifex.Utils;

namespace Pontifex.DeliveryManager
{
    internal class WireMessageSerializer : IWireMessageSerializer
    {
        // Types are no longer used — discriminator is now bool(false) for
        // DeliveryInfo and partsNumber (byte) for user messages.
        // Constants kept for reference but unused in production code.
        private const byte TypeUserSingle = 0;
        private const byte TypeUserMulti = 1;
        private const byte TypeDeliveryInfo = 2;

        private readonly ICollectablePool _pool;

        public WireMessageSerializer(ICollectablePool pool)
        {
            _pool = pool;
        }

        public int UserSingleOverhead => 6;
        public int UserMultiOverhead => 10;
        public int DeliveryInfoFixedOverhead => 6;
        public int DeliveryInfoElementSize => 5;

        public UnionDataList CreateUserSingle(DeliveryId id, IMultiRefByteArray data)
        {
            var msg = _pool.Acquire<UnionDataList>();
            msg.PutLast(new UnionData((byte)1));    // partsNumber = 1 (single chunk)
            msg.PutLast(new UnionData(id.Id));
            msg.PutLast(new UnionData((IMultiRefReadOnlyByteArray)data.Acquire()));
            return msg;
        }

        public UnionDataList CreateUserMulti(DeliveryId id, IMultiRefByteArray chunkData, byte partId, byte partsNumber)
        {
            var msg = _pool.Acquire<UnionDataList>();
            msg.PutLast(new UnionData(partsNumber));
            msg.PutLast(new UnionData(partId));
            msg.PutLast(new UnionData(id.Id));
            msg.PutLast(new UnionData((IMultiRefReadOnlyByteArray)chunkData.Acquire()));
            return msg;
        }

        public UnionDataList CreateDeliveryInfo(IReadOnlyList<DeliveryInfo> confirmations, int start, int count)
        {
            var msg = _pool.Acquire<UnionDataList>();
            msg.PutLast(new UnionData(false));       // isUser = false
            msg.PutLast(new UnionData((ushort)count));

            for (int i = start; i < start + count; ++i)
            {
                msg.PutLast(new UnionData(confirmations[i].Id.Id));
                msg.PutLast(new UnionData(confirmations[i].ChunkId));
            }

            return msg;
        }

        public bool TryParseDeliveryInfo(UnionDataList data, List<DeliveryInfo> confirmations)
        {
            if (!data.TryPopFirst(out bool isUser) || isUser)
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

        public bool TryParseUserMessage(UnionDataList data, out ParsedUserMessage result)
        {
            result = default;

            if (!data.TryPopFirst(out byte partsNumber))
                return false;

            byte partId = 0;
            if (partsNumber > 1)
            {
                if (!data.TryPopFirst(out partId))
                    return false;
            }

            if (!data.TryPopFirst(out ushort id))
                return false;

            if (!data.TryPopFirst(out IMultiRefReadOnlyByteArray? payload) || payload == null)
                return false;

            result = new ParsedUserMessage(
                type: 0,
                new DeliveryId(id),
                partId,
                partsNumber,
                isMultiChunk: partsNumber > 1,
                payload);

            return true;
        }
    }
}
