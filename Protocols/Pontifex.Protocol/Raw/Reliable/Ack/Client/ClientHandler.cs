using System;
using System.Collections.Generic;
using Actuarius.Memory;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Raw.Reliable.Ack.Protocols
{
    internal class ClientHandler : IRawReliableAckClientHandler, IRawReliableAckClientSideEndpoint
    {
        private readonly RawReliableAckWrapperClient mTransport;
        private readonly IRawReliableAckWrapperClientLogic mWrapperLogic;
        private readonly IRawReliableAckClientHandler mUserHandler;

        private volatile IRawReliableAckClientSideEndpoint? mTransportEndpoint;

        private readonly object mSendCallSerializer = new object();

        public ClientHandler(RawReliableAckWrapperClient transport, IRawReliableAckWrapperClientLogic wrapperLogic, IRawReliableAckClientHandler userHandler)
        {
            mTransport = transport;
            mWrapperLogic = wrapperLogic;
            mUserHandler = userHandler;
        }

        void IRawAckClientHandler.FillAckData(UnionDataList ackData)
        {
            mUserHandler.FillAckData(ackData);
            mWrapperLogic.UpdateAckData(ackData);
        }

        void IRawReliableAckClientHandler.OnConnected(IRawReliableAckClientSideEndpoint endPoint, UnionDataList ackResponse)
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
                mTransport.FailException("IRawAckClientHandler.OnConnected", ex);
            }
        }

        void IRawAckBaseHandler.OnDisconnected(StopReason reason)
        {
            try
            {
                mUserHandler.OnDisconnected(reason);
                mWrapperLogic.OnDisconnected();
            }
            catch (Exception ex)
            {
                mTransport.FailException("IRawAckClientHandler.OnDisconnected", ex);
            }
            mTransport.Stop(reason);
            mTransportEndpoint = null;
        }

        void IRawAckClientHandler.OnStopped(StopReason reason)
        {
            mUserHandler.OnStopped(reason: reason);
            mTransport.Stop(reason);
        }

        void IRawAckBaseHandler.OnReceived(UnionDataList receivedBuffer)
        {
            try
            {
                if (mWrapperLogic.ProcessReceivedData(receivedBuffer))
                {
                    mUserHandler.OnReceived(receivedBuffer.Acquire());
                    return;
                }

                mTransport.Fail("IRawAckClientHandler.OnReceived", "Failed to process incoming data");
            }
            catch (Exception ex)
            {
                mTransport.FailException("IRawAckClientHandler.OnReceived", ex, "Failed to process received data");
            }
            finally
            {
                receivedBuffer.Release();
            }
        }

        IEndPoint? IRawAckBaseEndpoint.RemoteEndPoint => mTransportEndpoint?.RemoteEndPoint;

        bool IRawAckBaseEndpoint.IsConnected
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

        bool IRawAckBaseEndpoint.Disconnect(StopReason reason)
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

        int IRawAckBaseEndpoint.MessageMaxByteSize
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

        SendResult IRawReliableAckBaseEndpoint.Send(UnionDataList bufferToSend)
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
