using Actuarius.Memory;
using Pontifex.Abstractions.Endpoints.Server;
using Pontifex.Abstractions.Handlers;
using Pontifex.Abstractions.Handlers.Server;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Api
{
    public class ServerSideApi<TApi> : IAckRawServerHandler
        where TApi : IApiRoot
    {
        private readonly TApi _api;
        private readonly IMemoryRental _memoryRental;
        private readonly ILogger Log;
        
        private IAckRawClientEndpoint? _endpoint;
        private TransportPipeSystem? _transportPipeSystem;
        
        public TApi Api => _api;
        
        public ServerSideApi(TApi api, IMemoryRental memoryRental, ILogger logger) 
        {
            _api = api;
            _memoryRental = memoryRental;
            Log = logger;
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
            }, _memoryRental, Log);
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