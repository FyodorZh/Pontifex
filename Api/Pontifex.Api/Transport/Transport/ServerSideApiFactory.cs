using System;
using Actuarius.Memory;
using Pontifex.Abstractions.Acknowledgers;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Api
{
    public class ServerSideApiFactory<TApi> : IRawServerAcknowledger<ServerSideApi>
        where TApi : IApiRoot
    {
        private readonly Func<TApi> _apiFactory;
        private readonly IMemoryRental _memoryRental;
        private readonly ILogger Log;

        public ServerSideApiFactory(Func<TApi> apiFactory, IMemoryRental memoryRental, ILogger logger)
        {
            _apiFactory = apiFactory;
            _memoryRental = memoryRental;
            Log = logger;
        }
        
        public ServerSideApi? TryAck(UnionDataList ackData)
        {
            using var disposer = ackData.AsDisposable();
            if (ackData.TryPopFirst(out long value) && value == 777)
            {
                return new ServerSideApi(_apiFactory.Invoke(), _memoryRental, Log);
            }

            return null;
        }
    }
}