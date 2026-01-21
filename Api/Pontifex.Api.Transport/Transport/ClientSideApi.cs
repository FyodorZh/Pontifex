using Actuarius.Memory;
using Pontifex.Abstractions.Endpoints.Client;
using Pontifex.Abstractions.Handlers;
using Pontifex.Abstractions.Handlers.Client;
using Pontifex.StopReasons;
using Pontifex.Utils;

namespace Pontifex.Api
{
    public class ClientSideApi : IAckRawClientHandler
    {
        private readonly IApiRoot _api;
        private readonly IMemoryRental _memoryRental;
        
        private TransportPipeSystem? _transportPipeSystem;
        private IAckRawServerEndpoint? _endpoint;

        //private volatile StopReason _currentReasonToStop = StopReason.Void;
        
        public ClientSideApi(IApiRoot api, IMemoryRental memoryRental) 
        {
            _api = api;
            _memoryRental = memoryRental;
        }

        void IAckHandler.WriteAckData(UnionDataList ackData)
        {
            long protocolHash = 777;
            // Write Protocol name and hash
            ackData.PutFirst(protocolHash);
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
                }, _memoryRental);
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