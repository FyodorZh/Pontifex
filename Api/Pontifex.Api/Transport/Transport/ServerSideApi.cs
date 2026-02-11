using System;
using Actuarius.Memory;
using Pontifex.Abstractions.Acknowledgers;
using Pontifex.Utils;

namespace Pontifex.Api
{
    public class ServerSideApiFactory<TApi> : IRawServerAcknowledger<ServerSideApiInstance<TApi>>
        where TApi : IApiRoot
    {
        private readonly Func<UnionDataList, ServerSideApiInstance<TApi>> _instanceFactory;

        public ServerSideApiFactory(Func<UnionDataList, ServerSideApiInstance<TApi>> instanceFactory)
        {
            _instanceFactory = instanceFactory;
        }
        
        public ServerSideApiInstance<TApi>? TryAck(UnionDataList ackData)
        {
            using var disposer = ackData.AsDisposable();
            if (ackData.TryPopFirst(out long apiHash) && apiHash == 777)
            {
                return _instanceFactory.Invoke(ackData);
            }

            return null;
        }
    }
}