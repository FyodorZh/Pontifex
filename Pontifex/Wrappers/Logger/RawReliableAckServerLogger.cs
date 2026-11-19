using System;
using System.Collections.Generic;
using Actuarius.Memory;
using Pontifex.Raw.Reliable.Logger;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Raw.Reliable.Ack.Logger
{
    public class RawReliableAckServerLogger : IRawReliableAckServer, IRawReliableAckServerAcknowledger<IRawReliableAckServerHandler>, IRawReliableAckServerHandler
    {
        private readonly IRawReliableAckServer _core;
        private readonly RawReliableServerHandlerLogger _handlerLogger;

        private IRawReliableAckServerAcknowledger<IRawReliableAckServerHandler>? _userAcknowledger;
        
        public TransportType Type => TransportType.RawReliableAck;

        public RawReliableAckServerLogger(IRawReliableAckServer core)
        {
            _core = core;
            _handlerLogger = new RawReliableServerHandlerLogger(core.Log, null);
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

        bool IRawReliableAckServer.Init(IRawReliableAckServerAcknowledger<IRawReliableAckServerHandler> acknowledger)
        {
            Log.i("Init()");
            _userAcknowledger = acknowledger;
            return _core.Init(this);
        }

        int IRawTransport.MessageMaxByteSize => _core.MessageMaxByteSize;

        public (IRawReliableAckServerHandler handler, UnionDataList ackResponse)? TryAck(
            IEndPoint remote, UnionDataList ackData)
        {
            if (_userAcknowledger == null)
            {
                Log.e("UserAcknowledger is null");
                ackData.Release();
                return null;
            }
            Log.i("UserAcknowledger.TryAck(" + ackData + ")");
            var result = _userAcknowledger.TryAck(remote, ackData);
            if (result == null)
            {
                return null;
            }

            _handlerLogger.UserHandler = result.Value.handler;
            Log.i("UserAcknowledger returned the session handler and the ACK response");
            return (this, result.Value.ackResponse);
        }

        void IRawReliableHandler.OnDisconnected(StopReason reason)
        {
            _handlerLogger.OnDisconnected(reason);
        }

        public void OnStopped(StopReason reason)
        {
            _handlerLogger.OnStopped(reason);
        }

        void IRawHandler.OnReceived(UnionDataList receivedBuffer)
        {
            _handlerLogger.OnReceived(receivedBuffer);
        }

        void IRawReliableServerHandler.OnConnected(IRawReliableEndpoint endPoint)
        {
            _handlerLogger.OnConnected(endPoint);
        }
    }
}
