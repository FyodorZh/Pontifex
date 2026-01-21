using System;
using Actuarius.Memory;
using Pontifex.Abstractions.Acknowledgers;
using Pontifex.Utils;

namespace Pontifex.Api
{
    public class ServerSideApiFactory<TApi> : IRawServerAcknowledger<ServerSideApi>
        where TApi : IApiRoot
    {
        private readonly Func<TApi> _apiFactory;
        private readonly IMemoryRental _memoryRental;

        public ServerSideApiFactory(Func<TApi> apiFactory, IMemoryRental memoryRental)
        {
            _apiFactory = apiFactory;
            _memoryRental = memoryRental;
        }
        
        public ServerSideApi? TryAck(UnionDataList ackData)
        {
            using var disposer = ackData.AsDisposable();
            if (ackData.TryPopFirst(out long value) && value == 7777)
            {
                return new ServerSideApi(_apiFactory.Invoke(), _memoryRental);
            }

            return null;
        }
    }
}