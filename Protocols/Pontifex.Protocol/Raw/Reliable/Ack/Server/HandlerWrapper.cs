using System;
using System.Collections.Generic;
using Actuarius.Memory;
using Pontifex.Utils;

namespace Pontifex.Raw.Reliable.Ack.Protocols
{
    public interface IHandlerWrapper : IRawReliableAckServerHandler
    {
        void Init(IRawReliableAckServerHandler wrappedHandler);
        bool CheckAckData(UnionDataList ackData);
    }


    public class HandlerWrapper<TLogic> : HandlerWrapper
        where TLogic : IRawReliableAckWrapperServerLogic
    {
        public HandlerWrapper(Func<TLogic> constructor)
            : base(constructor.Invoke())
        {
        }
    }

    public abstract class HandlerWrapper : IHandlerWrapper, IRawReliableAckServerSideEndpoint
    {
        private readonly IRawReliableAckWrapperServerLogic _logic;

        private volatile IRawReliableAckServerHandler _wrappedHandler = null!;

        private volatile IRawReliableAckServerSideEndpoint? _wrappedEndpoint;

        private readonly object mSendCallSerializer = new ();

        protected HandlerWrapper(IRawReliableAckWrapperServerLogic logic)
        {
            _logic = logic;
        }

        public void Init(IRawReliableAckServerHandler wrappedHandler)
        {
            _wrappedHandler = wrappedHandler;
        }

        public bool CheckAckData(UnionDataList ackData)
        {
            return _logic.ProcessAckData(ackData);
        }

        void IRawAckServerHandler.FillAckResponse(UnionDataList ackResponse)
        {
            _wrappedHandler.FillAckResponse(ackResponse);
        }

        void IRawReliableAckServerHandler.OnConnected(IRawReliableAckServerSideEndpoint endPoint)
        {
            _wrappedEndpoint = endPoint;
            _wrappedHandler.OnConnected(this);
            _logic.OnConnected();
        }

        void IRawAckBaseHandler.OnDisconnected(StopReason reason)
        {
            _logic.OnDisconnected();
            _wrappedHandler.OnDisconnected(reason);
            _wrappedEndpoint = null;
        }

        void IRawAckBaseHandler.OnReceived(UnionDataList receivedBuffer)
        {
            try
            {
                if (_logic.ProcessReceivedData(receivedBuffer))
                {
                    _wrappedHandler.OnReceived(receivedBuffer.Acquire());
                    return; // OK
                }

                // Failed
                var endpoint = _wrappedEndpoint;
                if (endpoint != null)
                {
                    endpoint.Disconnect(new StopReasons.TextFail("???", "Failed to process received data"));
                }
            }
            finally
            {
                receivedBuffer.Release();
            }
        }

        IEndPoint? IRawAckBaseEndpoint.RemoteEndPoint => _wrappedEndpoint?.RemoteEndPoint;

        bool IRawAckBaseEndpoint.IsConnected
        {
            get
            {
                var endpoint = _wrappedEndpoint;
                if (endpoint != null)
                {
                    return endpoint.IsConnected;
                }
                return false;
            }
        }

        int IRawAckBaseEndpoint.MessageMaxByteSize
        {
            get
            {
                var endpoint = _wrappedEndpoint;
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
                    var endpoint = _wrappedEndpoint;
                    if (endpoint != null)
                    {
                        if (_logic.ProcessSentData(bufferToSend))
                        {
                            return endpoint.Send(bufferToSend.Acquire());
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

        bool IRawAckBaseEndpoint.Disconnect(StopReason reason)
        {
            var endpoint = _wrappedEndpoint;
            if (endpoint != null)
            {
                return endpoint.Disconnect(reason);
            }
            return false;
        }
        
        void IBaseEndpoint.GetControls(List<IControl> dst, Predicate<IControl>? predicate)
        {
            _logic.GetControls(dst, predicate);
        }
    }
}
