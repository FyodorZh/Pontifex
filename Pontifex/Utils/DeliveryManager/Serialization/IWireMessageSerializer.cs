using System.Collections.Generic;
using Actuarius.Memory;
using Pontifex.Utils;

namespace Pontifex.DeliveryManager
{
    internal interface IWireMessageSerializer
    {
        int UserSingleOverhead { get; }
        int UserMultiOverhead { get; }

        UnionDataList CreateUserSingle(DeliveryId id, IMultiRefByteArray data);
        UnionDataList CreateUserMulti(DeliveryId id, IMultiRefByteArray chunkData, byte partId, byte partsNumber);

        bool TryParseUserMessage(UnionDataList data, out ParsedUserMessage result);
    }
}
