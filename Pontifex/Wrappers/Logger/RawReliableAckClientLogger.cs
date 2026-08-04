using System;
using System.Collections.Generic;
using Actuarius.Memory;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Raw.Reliable.Ack.Logger
{
    internal class RawReliableAckClientLogger : IRawReliableAckClient, IRawReliableAckClientHandler
    {
        private readonly IRawReliableAckClient _core;

        private volatile IRawReliableAckClientHandler? _userHandler;
        
        public TransportType Type => TransportType.RawReliableAck;
        
        public RawReliableAckClientLogger(IRawReliableAckClient core)
        {
            _core = core;
        }

        string ITransport.Name => _core.Name;

        bool ITransport.IsValid => _core.IsValid;

        bool ITransport.IsStarted => _core.IsStarted;

        bool ITransport.Start(Action<StopReason> onStopped)
        {
            _core.Log.i("Start()");
            return _core.Start(reason =>
            {
                _core.Log.i("OnStopped(" + reason + ")");
                onStopped(reason);
            });
        }

        bool ITransport.Stop(StopReason? reason)
        {
            _core.Log.i("Stop(" + reason + ")");
            return _core.Stop(reason);
        }
        
        ILogger ITransport.Log => _core.Log;

        IMemoryRental ITransport.Memory => _core.Memory;
        
        public void GetControls(List<IControl> dst, Predicate<IControl>? predicate = null)
        {
            _core.GetControls(dst, predicate);
        }

        bool IRawReliableAckClient.Init(IRawReliableAckClientHandler handler)
        {
            _userHandler = handler;
            _core.Log.i("Init()");
            return _core.Init(this);
        }

        int IRawTransport.MessageMaxByteSize => _core.MessageMaxByteSize;

        void IRawReliableHandler.OnDisconnected(StopReason reason)
        {
            _core.Log.i("UserHandler.OnDisconnected(" + reason + ")");
            _userHandler?.OnDisconnected(reason);
        }

        void IRawHandler.OnReceived(UnionDataList receivedBuffer)
        {
            _core.Log.i("UserHandler.OnReceived(" + receivedBuffer + ")");
            _userHandler?.OnReceived(receivedBuffer);
        }

        void IRawReliableAckClientHandler.FillAckData(UnionDataList ackData)
        {
            _core.Log.i("UserHandler.GetAckData()");
            _userHandler?.FillAckData(ackData);
        }

        void IRawReliableAckClientHandler.OnConnected(IRawReliableEndpoint endPoint, UnionDataList ackResponse)
        {
            _core.Log.i("UserHandler.OnConnected(" + endPoint + ", " + ackResponse + ")");
            var endPointWrapper = new RawAckClientSideEndpointWrapper(endPoint, (endpoint, dataToSend) =>
            {
                _core.Log.i("EndPoint.Send(" + dataToSend + ")");
                if (endpoint == null)
                {
                    return SendResult.Error;
                }

                var res = endpoint.Send(dataToSend);
                _core.Log.i("Result: " + res);
                return res;
            }, (endpoint, disconnectReason) =>
            {
                _core.Log.i("EndPoint.Disconnect(" + disconnectReason + ")");
                return endpoint?.Disconnect(disconnectReason) ?? false;
            });
            _userHandler?.OnConnected(endPointWrapper, ackResponse);
        }

        void IRawReliableClientHandler.OnStopped(StopReason reason)
        {
            _core.Log.i("UserHandler.OnStopped(" + reason + ")");
            _userHandler?.OnStopped(reason: reason);
        }
    }
}