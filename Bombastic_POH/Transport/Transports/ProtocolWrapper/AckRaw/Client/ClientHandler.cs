using System;
using Shared;
using Transport.Abstractions;
using Transport.Abstractions.Endpoints;
using Transport.Abstractions.Endpoints.Client;
using Transport.Abstractions.Handlers;
using Transport.Abstractions.Handlers.Client;
using Shared.Buffer;
using Transport.Endpoints;

namespace Transport.Transports.ProtocolWrapper.AckRaw
{
    internal class ClientHandler : IAckRawClientHandler, IAckRawServerEndpoint
    {
        private readonly AckRawWrapperClient mTransport;
        private readonly IAckRawWrapperClientLogic mWrapperLogic;
        private readonly IAckRawClientHandler mUserHandler;

        private volatile IAckRawServerEndpoint mTransportEndpoint;

        private readonly object mSendCallSerializer = new object();

        public ClientHandler(AckRawWrapperClient transport, IAckRawWrapperClientLogic wrapperLogic, IAckRawClientHandler userHandler)
        {
            mTransport = transport;
            mWrapperLogic = wrapperLogic;
            mUserHandler = userHandler;
        }

        byte[] IAckHandler.GetAckData()
        {
            byte[] userAck = mUserHandler.GetAckData();
            if (userAck != null)
            {
                byte[] ack = mWrapperLogic.UpdateAckData(userAck);
                return ack;
            }
            return null;
        }

        void IAckRawClientHandler.OnConnected(IAckRawServerEndpoint endPoint, ByteArraySegment ackResponse)
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

        void IRawBaseHandler.OnReceived(IMemoryBufferHolder receivedBuffer)
        {
            try
            {
                using (var bufferAccessor = receivedBuffer.ExposeAccessorOnce())
                {
                    if (mWrapperLogic.ProcessReceivedData(bufferAccessor.Buffer))
                    {
                        mUserHandler.OnReceived(bufferAccessor.Acquire());
                        return;
                    }
                    mTransport.Fail("IAckRawClientHandler.OnReceived", "Failed to process incomming data");
                }
            }
            catch (Exception ex)
            {
                mTransport.FailException("IAckRawClientHandler.OnReceived", ex, "Failed to process received data");
            }
        }

        IEndPoint IAckRawBaseEndpoint.RemoteEndPoint
        {
            get
            {
                var endpoint = mTransportEndpoint;
                if (endpoint != null)
                {
                    return endpoint.RemoteEndPoint;
                }
                return VoidEndPoint.Instance;
            }
        }

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

        SendResult IAckRawBaseEndpoint.Send(IMemoryBufferHolder bufferToSend)
        {
            lock (mSendCallSerializer)
            {
                using (var bufferAccessor = bufferToSend.ExposeAccessorOnce())
                {
                    var endpoint = mTransportEndpoint;
                    if (endpoint != null)
                    {
                        try
                        {
                            if (mWrapperLogic.ProcessSentData(bufferAccessor.Buffer))
                            {
                                return endpoint.Send(bufferAccessor.Acquire());
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
            }
        }
    }
}
