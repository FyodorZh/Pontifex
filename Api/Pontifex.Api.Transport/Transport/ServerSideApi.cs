using Actuarius.Memory;
using Pontifex.Abstractions.Endpoints.Server;
using Pontifex.Abstractions.Handlers.Server;
using Pontifex.Utils;

namespace Pontifex.Api
{
    public class ServerSideApi : AnySideApi, IAckRawServerHandler
    {
        public ServerSideApi(ProtocolApi api, IMemoryRental memoryRental) 
            : base(true, api, memoryRental)
        {
        }

        void IAckRawServerHandler.GetAckResponse(UnionDataList ackData)
        {
            throw new System.NotImplementedException();
        }

        void IAckRawServerHandler.OnConnected(IAckRawClientEndpoint endPoint)
        {
            throw new System.NotImplementedException();
        }
    }
}