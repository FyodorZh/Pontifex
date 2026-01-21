using Actuarius.Memory;
using Pontifex.Abstractions.Endpoints.Server;
using Pontifex.Abstractions.Handlers;
using Pontifex.Abstractions.Handlers.Server;
using Pontifex.Utils;

namespace Pontifex.Api
{
    public class ServerSideApi : IAckRawServerHandler
    {
        private readonly IApiRoot _api;
        private readonly IMemoryRental _memoryRental;
        
        private IAckRawClientEndpoint? _endpoint;
        private TransportPipeSystem? _transportPipeSystem;
        
        public ServerSideApi(IApiRoot api, IMemoryRental memoryRental) 
        {
            _api = api;
            _memoryRental = memoryRental;
        }

        void IAckRawServerHandler.GetAckResponse(UnionDataList ackData)
        {
            ackData.PutFirst((long)7777);
        }

        void IAckRawServerHandler.OnConnected(IAckRawClientEndpoint endPoint)
        {
            _endpoint = endPoint;
            
            _transportPipeSystem = new TransportPipeSystem(dataToSend =>
            {
                var endpoint = _endpoint;
                if (endpoint != null)
                {
                    return endpoint.Send(dataToSend);
                }
                dataToSend.Release();
                return SendResult.NotConnected;
            }, _memoryRental);
            _api.Disconnected += r => _endpoint?.Disconnect(r);
            _api.Start(true, _transportPipeSystem);
        }
        
        void IRawBaseHandler.OnReceived(UnionDataList receivedBuffer)
        {
            _transportPipeSystem!.OnReceived(receivedBuffer);
        }
        
        void IRawBaseHandler.OnDisconnected(StopReason reason)
        {
            _api.Stop();
            _transportPipeSystem = null;
            _endpoint = null;
        }
    }
}