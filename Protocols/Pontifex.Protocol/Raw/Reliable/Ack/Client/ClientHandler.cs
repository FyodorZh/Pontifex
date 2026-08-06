using System;
using System.Collections.Generic;
using Actuarius.Memory;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Raw.Reliable.Ack.Protocols
{
    internal class ClientHandler : IRawReliableAckClientHandler, IRawReliableEndpoint
    {
        private readonly RawReliableAckWrapperClient mTransport;
        private readonly IRawReliableAckWrapperClientLogic mWrapperLogic;
        private readonly IRawReliableAckClientHandler mUserHandler;

        private volatile IRawReliableEndpoint? mTransportEndpoint;

        private readonly object mSendCallSerializer = new object();

        public ClientHandler(RawReliableAckWrapperClient transport, IRawReliableAckWrapperClientLogic wrapperLogic, IRawReliableAckClientHandler userHandler)
        {
            mTransport = transport;
            mWrapperLogic = wrapperLogic;
            mUserHandler = userHandler;
        }

        void IRawReliableAckClientHandler.FillAckData(UnionDataList ackData)
        {
            mUserHandler.FillAckData(ackData);
            mWrapperLogic.UpdateAckData(ackData);
        }

        void IRawReliableAckClientHandler.OnConnected(IRawReliableEndpoint endPoint, UnionDataList ackResponse)
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

        void IRawReliableHandler.OnDisconnected(StopReason reason)
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

        void IRawReliableClientHandler.OnStopped(StopReason reason)
        {
            mUserHandler.OnStopped(reason: reason);
            mTransport.Stop(reason);
        }

        void IRawHandler.OnReceived(UnionDataList receivedBuffer)
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

        IEndPoint? IRawEndpoint.RemoteEndPoint => mTransportEndpoint?.RemoteEndPoint;

        bool IRawReliableEndpoint.IsConnected
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

        bool IRawReliableEndpoint.Disconnect(StopReason reason)
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

        int IRawEndpoint.MessageMaxByteSize
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

        SendResult IRawReliableEndpoint.Send(UnionDataList bufferToSend)
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
