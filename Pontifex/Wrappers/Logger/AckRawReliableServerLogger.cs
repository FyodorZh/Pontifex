using System;
using System.Collections.Generic;
using Actuarius.Memory;
using Pontifex.Ack.Raw;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Ack.Raw.Reliable.Logger
{
    public class AckRawReliableServerLogger : IAckRawReliableServer, IRawServerAcknowledger<IAckRawReliableServerHandler>, IAckRawReliableServerHandler
    {
        private readonly IAckRawReliableServer _core;

        private IRawServerAcknowledger<IAckRawReliableServerHandler>? _userAcknowledger;
        private IAckRawReliableServerHandler? _userHandler;
        
        public TransportType Type => TransportType.AckRawReliable;

        public AckRawReliableServerLogger(IAckRawReliableServer core)
        {
            _core = core;
        }

        string ITransport.Name => _core.Name;

        bool ITransport.IsValid => _core.IsValid;

        bool ITransport.IsStarted => _core.IsStarted;

        bool ITransport.Start(Action<StopReason> onStopped)
        {
            Log.i("Start()");
            return _core.Start(reason =>
            {
                Log.i("OnStopped(" + reason + ")");
                onStopped(reason);
            });
        }

        bool ITransport.Stop(StopReason? reason)
        {
            Log.i("Stop(" + reason + ")");
            return _core.Stop(reason);
        }
        
        ILogger ITransport.Log => _core.Log;

        IMemoryRental ITransport.Memory => _core.Memory;
        
        public void GetControls(List<IControl> dst, Predicate<IControl>? predicate = null)
        {
            _core.GetControls(dst, predicate);
        }

        bool IAckRawReliableServer.Init(IRawServerAcknowledger<IAckRawReliableServerHandler> acknowledger)
        {
            Log.i("Init()");
            _userAcknowledger = acknowledger;
            return _core.Init(this);
        }

        int IAckRawServer.MessageMaxByteSize => _core.MessageMaxByteSize;

        public IAckRawReliableServerHandler? TryAck(UnionDataList ackData)
        {
            if (_userAcknowledger == null)
            {
                Log.e("UserAcknowledger is null");
                return null;
            }
            Log.i("UserAcknowledger.TryAck(" + ackData + ")");
            _userHandler = _userAcknowledger.TryAck(ackData);
            if (_userHandler != null)
            {
                return this;
            }

            return null;
        }

        void IAckRawBaseHandler.OnDisconnected(StopReason reason)
        {
            Log.i("UserHandler.OnDisconnected(" + reason + ")");
            _userHandler?.OnDisconnected(reason);
        }

        void IAckRawBaseHandler.OnReceived(UnionDataList receivedBuffer)
        {
            Log.i("UserHandler.OnReceived(" + receivedBuffer + ")");
            _userHandler?.OnReceived(receivedBuffer);
        }

        void IAckRawServerHandler.FillAckResponse(UnionDataList ackResponse)
        {
            Log.i("UserHandler.GetAckResponse()");
            _userHandler?.FillAckResponse(ackResponse);
        }

        void IAckRawReliableServerHandler.OnConnected(IAckRawReliableServerSideEndpoint endPoint)
        {
            Log.i("UserHandler.OnConnected()");
            var endPointWrapper = new AckRawServerSideEndpointWrapper(endPoint, (endpoint, dataToSend) =>
            {
                Log.i("EndPoint.Send(" + dataToSend + ")");
                if (endpoint == null)
                {
                    return SendResult.Error;
                }

                var res = endpoint.Send(dataToSend);
                Log.i("Result: " + res);
                return res;
            }, (endpoint, disconnectReason) =>
            {
                Log.i("EndPoint.Disconnect(" + disconnectReason + ")");
                return endpoint?.Disconnect(disconnectReason) ?? false;
            }, 
                Array.Empty<IControl>());
            _userHandler?.OnConnected(endPointWrapper);
        }
    }
}