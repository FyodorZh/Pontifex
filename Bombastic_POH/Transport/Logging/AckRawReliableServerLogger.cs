using System;
using System.Collections.Generic;
using Pontifex.Utils;
using Transport.Abstractions;
using Transport.Abstractions.Acknowledgers;
using Transport.Abstractions.Endpoints;
using Transport.Abstractions.Endpoints.Server;
using Transport.Abstractions.Handlers;
using Transport.Abstractions.Handlers.Server;
using Transport.Abstractions.Servers;

namespace Transport
{
    public static class AckReliableRawServerWrapper_Ext
    {
        public static IAckReliableRawServer WrapWithLogger(this IAckReliableRawServer server, ILogger logger)
        {
            return new AckRawReliableServerLogger(server, logger);
        }
    }

    public class AckRawReliableServerLogger : IAckReliableRawServer, IRawServerAcknowledger<IAckRawServerHandler>, IAckRawServerHandler, IAckRawClientEndpoint
    {
        private readonly IAckReliableRawServer mCore;

        private IRawServerAcknowledger<IAckRawServerHandler> mUserAcknowledger;
        private IAckRawServerHandler mUserHandler;
        private IAckRawClientEndpoint mEndPoint;

        public ILogger Log { get; private set; }

        public AckRawReliableServerLogger(IAckReliableRawServer core, ILogger logger)
        {
            mCore = core;
            Log = logger;
        }

        TControl IControlProvider.TryGetControl<TControl>(string name)
        {
            Log.i("TryGetControl<" + typeof(TControl) + ">(" + (name != null ? name : "null") + ")");
            return mCore.TryGetControl<TControl>(name);
        }

        IEnumerable<TControl> IControlProvider.GetControls<TControl>(string name)
        {
            Log.i("TryGetControls<" + typeof(TControl) + ">(" + name + ")");
            return mCore.GetControls<TControl>(name);
        }

        string ITransport.Type
        {
            get { return mCore.Type; }
        }

        bool ITransport.IsValid
        {
            get { return mCore.IsValid; }
        }

        bool ITransport.IsStarted
        {
            get { return mCore.IsStarted; }
        }

        bool ITransport.Start(Action<StopReason> onStopped, ILogger logger)
        {
            Log.i("Start()");
            return mCore.Start(reason =>
            {
                Log.i("OnStopped(" + reason + ")");
                onStopped(reason);
            }, logger);
        }

        bool ITransport.Stop(StopReason reason)
        {
            Log.i("Stop(" + reason + ")");
            return mCore.Stop(reason);
        }

        bool IAckRawServer.Init(IRawServerAcknowledger<IAckRawServerHandler> acknowledger)
        {
            Log.i("Init()");
            mUserAcknowledger = acknowledger;
            return mCore.Init(this);
        }

        IEndPoint IAckRawBaseEndpoint.RemoteEndPoint
        {
            get { return mEndPoint.RemoteEndPoint; }
        }

        bool IAckRawBaseEndpoint.IsConnected
        {
            get { return mEndPoint.IsConnected; }
        }

        int IAckRawBaseEndpoint.MessageMaxByteSize
        {
            get { return mEndPoint.MessageMaxByteSize; }
        }

        SendResult IAckRawBaseEndpoint.Send(IMemoryBufferHolder bufferToSend)
        {
            Log.i("EndPoint.Send(" + bufferToSend + ")");
            var res = mEndPoint.Send(bufferToSend);
            Log.i("Result: " + res);
            return res;
        }

        bool IAckRawBaseEndpoint.Disconnect(StopReason reason)
        {
            Log.i("EndPoint.Disconnect(" + reason + ")");
            return mEndPoint.Disconnect(reason);
        }

        int IAckRawServer.MessageMaxByteSize
        {
            get { return mCore.MessageMaxByteSize; }
        }

        public IAckRawServerHandler TryAck(UnionDataList ackData, ILogger logger)
        {
            Log.i("UserAcknowledger.TryAck(" + ackData + ")");
            mUserHandler = mUserAcknowledger.TryAck(ackData, logger);
            if (mUserHandler != null)
            {
                return this;
            }

            return null;
        }

        void IRawBaseHandler.OnDisconnected(StopReason reason)
        {
            Log.i("UserHandler.OnDisconnected(" + reason + ")");
            mUserHandler.OnDisconnected(reason);
        }

        void IRawBaseHandler.OnReceived(IMemoryBufferHolder receivedBuffer)
        {
            Log.i("UserHandler.OnReceived(" + receivedBuffer + ")");
            mUserHandler.OnReceived(receivedBuffer);
        }

        byte[] IAckRawServerHandler.GetAckResponse()
        {
            Log.i("UserHandler.GetAckResponse()");
            return mUserHandler.GetAckResponse();
        }

        void IAckRawServerHandler.OnConnected(IAckRawClientEndpoint endPoint)
        {
            Log.i("UserHandler.OnConnected()");

            mEndPoint = endPoint;

            mUserHandler.OnConnected(this);
        }
    }
}