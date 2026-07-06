using System;
using System.Collections.Generic;
using Actuarius.Memory;
using Pontifex.Abstractions;
using Pontifex.Ack.Raw;
using Pontifex.Utils;

namespace Pontifex.Protocols
{
    public interface IHandlerWrapper : IAckRawReliableServerHandler
    {
        void Init(IAckRawReliableServerHandler wrappedHandler);
        bool CheckAckData(UnionDataList ackData);
    }


    public class HandlerWrapper<TLogic> : HandlerWrapper
        where TLogic : IAckRawWrapperServerLogic
    {
        public HandlerWrapper(Func<TLogic> constructor)
            : base(constructor.Invoke())
        {
        }
    }

    public abstract class HandlerWrapper : IHandlerWrapper, IAckRawReliableServerSideEndpoint
    {
        private readonly IAckRawWrapperServerLogic _logic;

        private volatile IAckRawReliableServerHandler _wrappedHandler = null!;

        private volatile IAckRawReliableServerSideEndpoint? _wrappedEndpoint;

        private readonly object mSendCallSerializer = new ();

        protected HandlerWrapper(IAckRawWrapperServerLogic logic)
        {
            _logic = logic;
        }

        public void Init(IAckRawReliableServerHandler wrappedHandler)
        {
            _wrappedHandler = wrappedHandler;
        }

        public bool CheckAckData(UnionDataList ackData)
        {
            return _logic.ProcessAckData(ackData);
        }

        void IAckRawServerHandler.FillAckResponse(UnionDataList ackResponse)
        {
            _wrappedHandler.FillAckResponse(ackResponse);
        }

        void IAckRawReliableServerHandler.OnConnected(IAckRawReliableServerSideEndpoint endPoint)
        {
            _wrappedEndpoint = endPoint;
            _wrappedHandler.OnConnected(this);
            _logic.OnConnected();
        }

        void IAckRawBaseHandler.OnDisconnected(StopReason reason)
        {
            _logic.OnDisconnected();
            _wrappedHandler.OnDisconnected(reason);
            _wrappedEndpoint = null;
        }

        void IAckRawBaseHandler.OnReceived(UnionDataList receivedBuffer)
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

        IEndPoint? IAckRawBaseEndpoint.RemoteEndPoint => _wrappedEndpoint?.RemoteEndPoint;

        bool IAckRawBaseEndpoint.IsConnected
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

        int IAckRawBaseEndpoint.MessageMaxByteSize
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

        SendResult IAckRawReliableBaseEndpoint.Send(UnionDataList bufferToSend)
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

        bool IAckRawBaseEndpoint.Disconnect(StopReason reason)
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
