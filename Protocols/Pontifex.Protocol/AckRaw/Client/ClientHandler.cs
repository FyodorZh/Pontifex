using System;
using System.Collections.Generic;
using Actuarius.Memory;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Ack.Raw.Reliable.Protocols
{
    internal class ClientHandler : IAckRawReliableClientHandler, IAckRawReliableClientSideEndpoint
    {
        private readonly AckRawWrapperClient mTransport;
        private readonly IAckRawWrapperClientLogic mWrapperLogic;
        private readonly IAckRawReliableClientHandler mUserHandler;

        private volatile IAckRawReliableClientSideEndpoint? mTransportEndpoint;

        private readonly object mSendCallSerializer = new object();

        public ClientHandler(AckRawWrapperClient transport, IAckRawWrapperClientLogic wrapperLogic, IAckRawReliableClientHandler userHandler)
        {
            mTransport = transport;
            mWrapperLogic = wrapperLogic;
            mUserHandler = userHandler;
        }

        void IAckRawClientHandler.FillAckData(UnionDataList ackData)
        {
            mUserHandler.FillAckData(ackData);
            mWrapperLogic.UpdateAckData(ackData);
        }

        void IAckRawReliableClientHandler.OnConnected(IAckRawReliableClientSideEndpoint endPoint, UnionDataList ackResponse)
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

        void IAckRawBaseHandler.OnDisconnected(StopReason reason)
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
            mUserHandler.OnStopped(reason: reason);
            mTransport.Stop(reason);
        }

        void IAckRawBaseHandler.OnReceived(UnionDataList receivedBuffer)
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

        void IBaseEndpoint.GetControls(List<IControl> dst, Predicate<IControl>? predicate)
        {
            mWrapperLogic.GetControls(dst, predicate);
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

        SendResult IAckRawReliableBaseEndpoint.Send(UnionDataList bufferToSend)
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
