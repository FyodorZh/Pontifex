using System;
using System.Collections.Generic;
using Actuarius.Memory;
using Pontifex.Utils;
using Transport.Abstractions;
using Transport.Abstractions.Clients;
using Transport.Abstractions.Endpoints;
using Transport.Abstractions.Endpoints.Client;
using Transport.Abstractions.Handlers;
using Transport.Abstractions.Handlers.Client;
using Transport.Endpoints;

namespace Transport
{
    internal class AckRawReliableClientLogger : IAckReliableRawClient, IAckRawClientHandler, IAckRawServerEndpoint
    {
        private readonly IAckReliableRawClient _core;

        private volatile IAckRawClientHandler? _userHandler;
        private volatile IAckRawServerEndpoint? _endPoint;
        
        public ILogger Log => _core.Log;

        public IMemoryRental Memory => _core.Memory;

        public AckRawReliableClientLogger(IAckReliableRawClient core)
        {
            _core = core;
        }

        TControl? IControlProvider.TryGetControl<TControl>(string? name) where TControl : class
        {
            Log.i("TryGetControl<" + typeof(TControl) + ">(" + (name ?? "null") + ")");
            return _core.TryGetControl<TControl>(name);
        }

        IEnumerable<TControl> IControlProvider.GetControls<TControl>(string? name)
        {
            Log.i("TryGetControls<" + typeof(TControl) + ">(" + name + ")");
            return _core.GetControls<TControl>(name);
        }

        string ITransport.Type => _core.Type;

        bool ITransport.IsValid => _core.IsValid;

        bool ITransport.IsStarted => _core.IsStarted;

        bool ITransport.Start(Action<StopReason> onStopped, ILogger logger)
        {
            Log.i("Start()");
            return _core.Start(reason =>
            {
                Log.i("OnStopped(" + reason + ")");
                onStopped(reason);
            }, logger);
        }

        bool ITransport.Stop(StopReason? reason)
        {
            Log.i("Stop(" + reason + ")");
            return _core.Stop(reason);
        }

        bool IAckRawClient.Init(IAckRawClientHandler handler)
        {
            _userHandler = handler;
            Log.i("Init()");
            return _core.Init(this);
        }

        IEndPoint IAckRawBaseEndpoint.RemoteEndPoint => _endPoint?.RemoteEndPoint ?? VoidEndPoint.Instance;

        bool IAckRawBaseEndpoint.IsConnected => _endPoint?.IsConnected ?? false;

        int IAckRawBaseEndpoint.MessageMaxByteSize => _endPoint?.MessageMaxByteSize ?? 0;

        SendResult IAckRawBaseEndpoint.Send(UnionDataList bufferToSend)
        {
            Log.i("EndPoint.Send(" + bufferToSend + ")");
            var endpoint = _endPoint;
            if (endpoint == null)
            {
                return SendResult.Error;
            }
            var res = endpoint.Send(bufferToSend);
            Log.i("Result: " + res);
            return res;
        }

        bool IAckRawBaseEndpoint.Disconnect(StopReason reason)
        {
            Log.i("EndPoint.Disconnect(" + reason + ")");
            return _endPoint?.Disconnect(reason) ?? false;
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
            _endPoint = endPoint;
            _userHandler?.OnConnected(endPoint, ackResponse);
        }

        void IAckRawClientHandler.OnStopped(StopReason reason)
        {
            Log.i("UserHandler.OnStopped(" + reason + ")");
            _userHandler?.OnStopped(reason);
        }

        void IHandler.Setup(IMemoryRental memory, ILogger logger)
        {
            _userHandler?.Setup(memory, logger);
        }
    }
}