using System;
using System.Collections.Generic;
using Actuarius.Memory;
using Pontifex.Abstractions;
using Pontifex.Abstractions.Acknowledgers;
using Pontifex.Abstractions.Endpoints.Server;
using Pontifex.Abstractions.Handlers;
using Pontifex.Abstractions.Handlers.Server;
using Pontifex.Abstractions.Servers;
using Pontifex.Utils;
using Scriba;

namespace Pontifex
{
    public class AckRawReliableServerLogger : IAckReliableRawServer, IRawServerAcknowledger<IAckRawServerHandler>, IAckRawServerHandler
    {
        private readonly IAckReliableRawServer _core;

        private IRawServerAcknowledger<IAckRawServerHandler>? _userAcknowledger;
        private IAckRawServerHandler? _userHandler;

        public ILogger Log => _core.Log;

        public IMemoryRental Memory => _core.Memory;

        public AckRawReliableServerLogger(IAckReliableRawServer core)
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

        bool IAckRawServer.Init(IRawServerAcknowledger<IAckRawServerHandler> acknowledger)
        {
            Log.i("Init()");
            _userAcknowledger = acknowledger;
            return _core.Init(this);
        }

        int IAckRawServer.MessageMaxByteSize => _core.MessageMaxByteSize;

        void IRawServerAcknowledger<IAckRawServerHandler>.Setup(IMemoryRental memory, ILogger logger)
        {
            _userAcknowledger?.Setup(memory, logger);
        }

        public IAckRawServerHandler? TryAck(UnionDataList ackData)
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

        void IAckRawServerHandler.GetAckResponse(UnionDataList ackResponse)
        {
            Log.i("UserHandler.GetAckResponse()");
            _userHandler?.GetAckResponse(ackResponse);
        }

        void IAckRawServerHandler.OnConnected(IAckRawClientEndpoint endPoint)
        {
            Log.i("UserHandler.OnConnected()");
            var endPointWrapper = new AckRawClientEndpointWrapper(endPoint, (endpoint, dataToSend) =>
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
            _userHandler?.OnConnected(endPointWrapper);
        }

        void IHandler.Setup(IMemoryRental memory, ILogger logger)
        {
            _userHandler?.Setup(memory, logger);
        }
    }
}