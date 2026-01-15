using Actuarius.Memory;
using Pontifex.Abstractions.Endpoints.Client;
using Pontifex.Abstractions.Handlers;
using Pontifex.Abstractions.Handlers.Client;
using Pontifex.StopReasons;
using Pontifex.Utils;

namespace Pontifex.Api
{
    public class ClientSideApi : AnySideApi, IAckRawClientHandler
    {
        public ClientSideApi(ProtocolApi api, IMemoryRental memoryRental) 
            : base(false, api, memoryRental)
        {
        }

        void IAckHandler.WriteAckData(UnionDataList ackData)
        {
            // Write Protocol name and hash
            ackData.PutFirst(777);
        }

        void IAckRawClientHandler.OnConnected(IAckRawServerEndpoint endPoint, UnionDataList ackResponse)
        {
            using var disposer = ackResponse.AsDisposable();
            if (ackResponse.TryPopFirst(out int value) && value == 7777)
            {
                OnConnected(endPoint);
            }
            else
            {
                endPoint.Disconnect(new AckRejected("protocol")); // todo source
            }
        }

        void IAckRawClientHandler.OnStopped(StopReason reason)
        {
            Stop(reason);
        }
    }
}