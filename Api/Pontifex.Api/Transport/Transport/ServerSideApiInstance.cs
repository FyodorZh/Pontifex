using System;
using Actuarius.Memory;
using Pontifex.Raw.Reliable.Ack;
using Pontifex.Endpoints;
using Pontifex.Raw.Reliable;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Api
{
    public class ServerSideApiInstance<TApi> : IRawReliableAckServerHandler
        where TApi : IApiRoot
    {
        private readonly TApi _api;
        private readonly IMemoryRental _memoryRental;
        private readonly ILogger Log;
        
        private IRawReliableEndpoint? _endpoint;
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

        void IRawReliableAckServerHandler.FillAckResponse(UnionDataList ackData)
        {
            ackData.PutFirst((long)7777);
        }

        void IRawReliableAckServerHandler.OnConnected(IRawReliableEndpoint endPoint)
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
        
        void IRawReliableHandler.OnReceived(UnionDataList receivedBuffer)
        {
            _transportPipeSystem!.OnReceived(receivedBuffer);
        }
        
        void IRawReliableHandler.OnDisconnected(StopReason reason)
        {
            ApiStopped?.Invoke(this);
            _api.Stop();
            _transportPipeSystem = null;
            _endpoint = null;
        }
    }
}