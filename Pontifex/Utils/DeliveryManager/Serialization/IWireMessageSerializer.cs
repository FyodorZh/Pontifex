using System.Collections.Generic;
using Actuarius.Memory;
using Pontifex.Utils;

namespace Pontifex.DeliveryManager
{
    internal interface IWireMessageSerializer
    {
        int UserSingleOverhead { get; }
        int UserMultiOverhead { get; }
        int DeliveryInfoFixedOverhead { get; }
        int DeliveryInfoElementSize { get; }

        UnionDataList CreateUserSingle(DeliveryId id, IMultiRefByteArray data);
        UnionDataList CreateUserMulti(DeliveryId id, IMultiRefByteArray chunkData, byte partId, byte partsNumber);
        UnionDataList CreateDeliveryInfo(IReadOnlyList<DeliveryInfo> confirmations, int start, int count);

        bool TryParseDeliveryInfo(UnionDataList data, List<DeliveryInfo> confirmations);
        bool TryParseUserMessage(UnionDataList data, out ParsedUserMessage result);
    }
}
