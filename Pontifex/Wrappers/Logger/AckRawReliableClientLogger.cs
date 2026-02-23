using System;
using System.Collections.Generic;
using Actuarius.Memory;
using Pontifex.Abstractions;
using Pontifex.Abstractions.Clients;
using Pontifex.Abstractions.Endpoints.Client;
using Pontifex.Abstractions.Handlers;
using Pontifex.Abstractions.Handlers.Client;
using Pontifex.Utils;
using Scriba;

namespace Pontifex
{
    internal class AckRawReliableClientLogger : IAckReliableRawClient, IAckRawClientHandler
    {
        private readonly IAckReliableRawClient _core;

        private volatile IAckRawClientHandler? _userHandler;
        
        public AckRawReliableClientLogger(IAckReliableRawClient core)
        {
            _core = core;
        }

        string ITransport.Type => _core.Type;

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

        bool IAckRawClient.Init(IAckRawClientHandler handler)
        {
            _userHandler = handler;
            Log.i("Init()");
            return _core.Init(this);
        }

        int IAckRawClient.MessageMaxByteSize => _core.MessageMaxByteSize;

        void IRawBaseHandler.OnDisconnected(StopReason reason)
        {
            Log.i("UserHandler.OnDisconnected(" + reason + ")");
            _userHandler?.OnDisconnected(reason);
        }

        void IRawBaseHandler.OnReceived(UnionDataList receivedBuffer)
        {
            Log.i("UserHandler.OnReceived(" + receivedBuffer + ")");
            _userHandler?.OnReceived(receivedBuffer);
        }

        void IAckHandler.WriteAckData(UnionDataList ackData)
        {
            Log.i("UserHandler.GetAckData()");
            _userHandler?.WriteAckData(ackData);
        }

        void IAckRawClientHandler.OnConnected(IAckRawServerEndpoint endPoint, UnionDataList ackResponse)
        {
            Log.i("UserHandler.OnConnected(" + endPoint + ", " + ackResponse + ")");
            var endPointWrapper = new AckRawServerEndpointWrapper(endPoint, (endpoint, dataToSend) =>
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
            });
            _userHandler?.OnConnected(endPointWrapper, ackResponse);
        }

        void IAckRawClientHandler.OnStopped(StopReason reason)
        {
            Log.i("UserHandler.OnStopped(" + reason + ")");
            _userHandler?.OnStopped(reason);
        }
    }
}