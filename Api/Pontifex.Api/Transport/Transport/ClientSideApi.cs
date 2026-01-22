using Actuarius.Memory;
using Pontifex.Abstractions.Endpoints.Client;
using Pontifex.Abstractions.Handlers;
using Pontifex.Abstractions.Handlers.Client;
using Pontifex.StopReasons;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Api
{
    public class ClientSideApi : IAckRawClientHandler
    {
        private readonly IApiRoot _api;
        private readonly IMemoryRental _memoryRental;
        private readonly ILogger Log;
        
        private TransportPipeSystem? _transportPipeSystem;
        private IAckRawServerEndpoint? _endpoint;
        
        protected virtual void AppendAckData(UnionDataList ackData)
        {
            // Override to add custom ack data
        }
        
        public ClientSideApi(IApiRoot api, IMemoryRental memoryRental, ILogger logger) 
        {
            _api = api;
            _memoryRental = memoryRental;
            Log = logger;
        }

        void IAckHandler.WriteAckData(UnionDataList ackData)
        {
            AppendAckData(ackData);
            long apiHash = 777;
            ackData.PutFirst(apiHash);
        }

        void IAckRawClientHandler.OnConnected(IAckRawServerEndpoint endPoint, UnionDataList ackResponse)
        {
            using var disposer = ackResponse.AsDisposable();
            if (ackResponse.TryPopFirst(out long value) && value == 7777)
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
                _api.Start(false, _transportPipeSystem);
            }
            else
            {
                endPoint.Disconnect(new AckRejected("protocol:" + _api.GetType()));
            }
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
        
        void IAckRawClientHandler.OnStopped(StopReason reason)
        {
            // DO NOTHING
        }
    }
}