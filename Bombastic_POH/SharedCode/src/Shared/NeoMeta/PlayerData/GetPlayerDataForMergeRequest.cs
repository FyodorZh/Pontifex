using Serializer.BinarySerializer;
using Shared.CommonData.Plt;

namespace Shared.NeoMeta.PlayerData
{
    public sealed class GetPlayerDataForMergeRequest : IDataStruct
    {
        public string Key;

        public GetPlayerDataForMergeRequest()
        {
        }

        public GetPlayerDataForMergeRequest(string key)
        {
            Key = key;
        }

        public bool Serialize(IBinarySerializer dst)
        {
            dst.Add(ref Key);

            return true;
        }

        public sealed class Response : IDataStruct
        {
            public long PlayerId;
            public ItemIdWithCount[] Currencies;

            public Response()
            {
            }

            public Response(long playerId, ItemIdWithCount[] currencies)
            {
                PlayerId = playerId;
                Currencies = currencies;
            }

            public bool Serialize(IBinarySerializer dst)
            {
                dst.Add(ref PlayerId);
                dst.Add(ref Currencies);

                return true;
            }
        }
    }
}
