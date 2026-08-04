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

    public abstract class HandlerWrapper : IHandlerWrapper, IRawReliableEndpoint
    {
        private readonly IRawReliableAckWrapperServerLogic _logic;

        private volatile IRawReliableAckServerHandler _wrappedHandler = null!;

        private volatile IRawReliableEndpoint? _wrappedEndpoint;

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

        void IRawReliableAckServerHandler.FillAckResponse(UnionDataList ackResponse)
        {
            _wrappedHandler.FillAckResponse(ackResponse);
        }

        void IRawReliableAckServerHandler.OnConnected(IRawReliableEndpoint endPoint)
        {
            _wrappedEndpoint = endPoint;
            _wrappedHandler.OnConnected(this);
            _logic.OnConnected();
        }

        void IRawReliableHandler.OnDisconnected(StopReason reason)
        {
            _logic.OnDisconnected();
            _wrappedHandler.OnDisconnected(reason);
            _wrappedEndpoint = null;
        }

        void IRawReliableHandler.OnReceived(UnionDataList receivedBuffer)
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

        IEndPoint? IRawEndpoint.RemoteEndPoint => _wrappedEndpoint?.RemoteEndPoint;

        bool IRawReliableEndpoint.IsConnected
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

        int IRawEndpoint.MessageMaxByteSize
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

        SendResult IRawReliableEndpoint.Send(UnionDataList bufferToSend)
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

        bool IRawReliableEndpoint.Disconnect(StopReason reason)
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
