using System;
using Actuarius.Memory;
using Pontifex.Abstractions.Acknowledgers;
using Pontifex.Utils;

namespace Pontifex.Api
{
    public class ServerSideApiFactory<TApi> : IRawServerAcknowledger<ServerSideApi<TApi>>
        where TApi : IApiRoot
    {
        private readonly Func<UnionDataList, ServerSideApi<TApi>> _apiFactory;

        public ServerSideApiFactory(Func<UnionDataList, ServerSideApi<TApi>> apiFactory)
        {
            _apiFactory = apiFactory;
        }
        
        public ServerSideApi<TApi>? TryAck(UnionDataList ackData)
        {
            using var disposer = ackData.AsDisposable();
            if (ackData.TryPopFirst(out long apiHash) && apiHash == 777)
            {
                return _apiFactory.Invoke(ackData);
            }

            return null;
        }
    }
}