using System;
using Actuarius.Memory;
using Pontifex.Ack;
using Pontifex.Ack.Raw;
using Pontifex.StopReasons;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Api
{
    public class ClientSideApi : IAckRawReliableClientHandler
    {
        private readonly IApiRoot _api;
        private readonly IMemoryRental _memoryRental;
        private readonly ILogger Log;
        
        private TransportPipeSystem? _transportPipeSystem;
        private IAckRawReliableClientSideEndpoint? _endpoint;

        private bool _wasConnectedEver;
        
        public event Action<IAckRawReliableClientSideEndpoint>? Connected;
        public event Action<StopReason>? Disconnected;
        
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

        void IAckRawClientHandler.FillAckData(UnionDataList ackData)
        {
            AppendAckData(ackData);
            long apiHash = 777;
            ackData.PutFirst(apiHash);
        }

        void IAckRawReliableClientHandler.OnConnected(IAckRawReliableClientSideEndpoint endPoint, UnionDataList ackResponse)
        {
            using var disposer = ackResponse.AsDisposable();
            if (ackResponse.TryPopFirst(out long value) && value == 7777)
            {
                _wasConnectedEver = true;
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
                Connected?.Invoke(endPoint);
            }
            else
            {
                endPoint.Disconnect(new AckRejected("protocol:" + _api.GetType()));
            }
        }
        
        void IAckRawBaseHandler.OnReceived(UnionDataList receivedBuffer)
        {
            _transportPipeSystem!.OnReceived(receivedBuffer);
        }

        void IAckRawBaseHandler.OnDisconnected(StopReason reason)
        {
            _api.Stop();
            _transportPipeSystem = null;
            _endpoint = null;
            Disconnected?.Invoke(reason);
        }
        
        void IAckRawClientHandler.OnStopped(StopReason reason)
        {
            if (!_wasConnectedEver) // prevent double invocation 
            {
                Disconnected?.Invoke(reason);
            }
        }
    }
}