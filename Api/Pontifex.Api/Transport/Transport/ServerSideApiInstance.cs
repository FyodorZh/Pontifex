using System;
using Actuarius.Memory;
using Pontifex.Ack.Raw;
using Pontifex.Ack.Raw.Reliable;
using Pontifex.Endpoints;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Api
{
    public class ServerSideApiInstance<TApi> : IAckRawReliableServerHandler
        where TApi : IApiRoot
    {
        private readonly TApi _api;
        private readonly IMemoryRental _memoryRental;
        private readonly ILogger Log;
        
        private IAckRawReliableServerSideEndpoint? _endpoint;
        private TransportPipeSystem? _transportPipeSystem;

        public event Action<ServerSideApiInstance<TApi>>? ApiStarted;
        public event Action<ServerSideApiInstance<TApi>>? ApiStopped;
        
        protected TApi Api => _api;

        public IEndPoint Endpoint => _endpoint?.RemoteEndPoint ?? VoidEndPoint.Instance;
        
        public ServerSideApiInstance(TApi api, IMemoryRental memoryRental, ILogger logger) 
        {
            _api = api;
            _memoryRental = memoryRental;
            Log = logger;
        }

        void IAckRawServerHandler.FillAckResponse(UnionDataList ackData)
        {
            ackData.PutFirst((long)7777);
        }

        void IAckRawReliableServerHandler.OnConnected(IAckRawReliableServerSideEndpoint endPoint)
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
            ApiStarted?.Invoke(this);
        }
        
        void IAckRawBaseHandler.OnReceived(UnionDataList receivedBuffer)
        {
            _transportPipeSystem!.OnReceived(receivedBuffer);
        }
        
        void IAckRawBaseHandler.OnDisconnected(StopReason reason)
        {
            ApiStopped?.Invoke(this);
            _api.Stop();
            _transportPipeSystem = null;
            _endpoint = null;
        }
    }
}