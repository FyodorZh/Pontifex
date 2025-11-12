using System;
using System.Collections.Generic;
using Shared;
using Shared.Buffer;
using Transport.Abstractions;
using Transport.Abstractions.Clients;
using Transport.Abstractions.Endpoints;
using Transport.Abstractions.Endpoints.Client;
using Transport.Abstractions.Handlers;
using Transport.Abstractions.Handlers.Client;

namespace Transport
{
    public static class AckReliableRawClientWrapper_Ext
    {
        public static IAckReliableRawClient WrapWithLogger(this IAckReliableRawClient client, ILogger logger)
        {
            return new AckReliableRawClientWrapper(client, logger);
        }
    }

    internal class AckReliableRawClientWrapper : IAckReliableRawClient, IAckRawClientHandler, IAckRawServerEndpoint
    {
        private readonly IAckReliableRawClient mCore;

        private IAckRawClientHandler mUserHandler;
        private IAckRawServerEndpoint mEndPoint;

        public ILogger Log { get; private set; }

        public AckReliableRawClientWrapper(IAckReliableRawClient core, ILogger logger)
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

        bool IAckRawClient.Init(IAckRawClientHandler handler)
        {
            mUserHandler = handler;
            Log.i("Init()");
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

        int IAckRawClient.MessageMaxByteSize
        {
            get { return mCore.MessageMaxByteSize; }
        }

        void IRawBaseHandler.OnDisconnected(StopReason reason)
        {
            Log.i("UserHandler.OnDisconnected(" + reason + ")");
            mUserHandler.OnDisconnected(reason);
        }

        void IRawBaseHandler.OnReceived(IMemoryBufferHolder receivedBuffer)
        {
            Log.i("UserHandler.OnReceived(" + receivedBuffer.ToString() + ")");
            mUserHandler.OnReceived(receivedBuffer);
        }

        byte[] IAckHandler.GetAckData()
        {
            Log.i("UserHandler.GetAckData()");
            return mUserHandler.GetAckData();
        }

        void IAckRawClientHandler.OnConnected(IAckRawServerEndpoint endPoint, ByteArraySegment ackResponse)
        {
            Log.i("UserHandler.OnConnected(" + endPoint + ", " + ackResponse + ")");
            mEndPoint = endPoint;
            mUserHandler.OnConnected(endPoint, ackResponse);
        }

        void IAckRawClientHandler.OnStopped(StopReason reason)
        {
            Log.i("UserHandler.OnStopped(" + reason + ")");
            mUserHandler.OnStopped(reason);
        }
    }
}