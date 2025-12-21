using System;
using Actuarius.Memory;
using Pontifex.Abstractions;
using Pontifex.Abstractions.Endpoints;
using Pontifex.Abstractions.Endpoints.Client;
using Pontifex.Abstractions.Handlers;
using Pontifex.Abstractions.Handlers.Client;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Protocols
{
    internal class ClientHandler : IAckRawClientHandler, IAckRawServerEndpoint
    {
        private readonly AckRawWrapperClient mTransport;
        private readonly IAckRawWrapperClientLogic mWrapperLogic;
        private readonly IAckRawClientHandler mUserHandler;

        private volatile IAckRawServerEndpoint? mTransportEndpoint;

        private readonly object mSendCallSerializer = new object();

        public ClientHandler(AckRawWrapperClient transport, IAckRawWrapperClientLogic wrapperLogic, IAckRawClientHandler userHandler)
        {
            mTransport = transport;
            mWrapperLogic = wrapperLogic;
            mUserHandler = userHandler;
        }

        void IAckHandler.WriteAckData(UnionDataList ackData)
        {
            mUserHandler.WriteAckData(ackData);
            mWrapperLogic.UpdateAckData(ackData);
        }

        void IAckRawClientHandler.OnConnected(IAckRawServerEndpoint endPoint, UnionDataList ackResponse)
        {
            mTransportEndpoint = endPoint;
            try
            {
                mTransport.ConnectionFinished_Internal(this, ackResponse);
                mWrapperLogic.OnConnected();
                mUserHandler.OnConnected(this, ackResponse);
            }
            catch (Exception ex)
            {
                mTransport.FailException("IAckRawClientHandler.OnConnected", ex);
            }
        }

        void IRawBaseHandler.OnDisconnected(StopReason reason)
        {
            try
            {
                mUserHandler.OnDisconnected(reason);
                mWrapperLogic.OnDisconnected();
            }
            catch (Exception ex)
            {
                mTransport.FailException("IAckRawClientHandler.OnDisconnected", ex);
            }
            mTransport.Stop(reason);
            mTransportEndpoint = null;
        }

        void IAckRawClientHandler.OnStopped(StopReason reason)
        {
            mUserHandler.OnStopped(reason);
            mTransport.Stop(reason);
        }

        void IRawBaseHandler.OnReceived(UnionDataList receivedBuffer)
        {
            try
            {
                if (mWrapperLogic.ProcessReceivedData(receivedBuffer))
                {
                    mUserHandler.OnReceived(receivedBuffer.Acquire());
                    return;
                }

                mTransport.Fail("IAckRawClientHandler.OnReceived", "Failed to process incoming data");
            }
            catch (Exception ex)
            {
                mTransport.FailException("IAckRawClientHandler.OnReceived", ex, "Failed to process received data");
            }
            finally
            {
                receivedBuffer.Release();
            }
        }

        IEndPoint? IAckRawBaseEndpoint.RemoteEndPoint => mTransportEndpoint?.RemoteEndPoint;

        bool IAckRawBaseEndpoint.IsConnected
        {
            get
            {
                var endpoint = mTransportEndpoint;
                if (endpoint != null)
                {
                    return endpoint.IsConnected;
                }
                return false;
            }
        }

        bool IAckRawBaseEndpoint.Disconnect(StopReason reason)
        {
            var endpoint = mTransportEndpoint;
            if (endpoint != null)
            {
                return endpoint.Disconnect(reason);
            }
            return false;
        }

        int IAckRawBaseEndpoint.MessageMaxByteSize
        {
            get
            {
                var endpoint = mTransportEndpoint;
                if (endpoint != null)
                {
                    return endpoint.MessageMaxByteSize;
                }
                return 0;
            }
        }

        SendResult IAckRawBaseEndpoint.Send(UnionDataList bufferToSend)
        {
            lock (mSendCallSerializer)
            {
                try
                {
                    var endpoint = mTransportEndpoint;
                    if (endpoint != null)
                    {
                        try
                        {
                            if (mWrapperLogic.ProcessSentData(bufferToSend))
                            {
                                return endpoint.Send(bufferToSend.Acquire());
                            }

                            return SendResult.Error;
                        }
                        catch (Exception ex)
                        {
                            Log.wtf(ex);
                        }
                    }

                    return SendResult.Error;
                }
                finally
                {
                    bufferToSend.Release();
                }
            }
        }
    }
}
