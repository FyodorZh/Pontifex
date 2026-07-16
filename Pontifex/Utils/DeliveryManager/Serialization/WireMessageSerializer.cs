using Actuarius.Memory;
using Pontifex.Utils;

namespace Pontifex.DeliveryManager
{
    internal class WireMessageSerializer : IWireMessageSerializer
    {
        private readonly ICollectablePool _pool;

        public WireMessageSerializer(ICollectablePool pool)
        {
            _pool = pool;
        }

        public int UserSingleOverhead => 6;
        public int UserMultiOverhead => 10;

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
