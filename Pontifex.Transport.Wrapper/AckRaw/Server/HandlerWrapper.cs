using System;
using Actuarius.Memory;
using Pontifex.Utils;
using Scriba;
using Transport.Abstractions;
using Transport.Abstractions.Endpoints;
using Transport.Abstractions.Endpoints.Server;
using Transport.Abstractions.Handlers;
using Transport.Abstractions.Handlers.Server;
using Transport.Endpoints;

namespace Transport.Transports.ProtocolWrapper.AckRaw
{
    public interface IHandlerWrapper : IAckRawServerHandler
    {
        void Init(IAckRawServerHandler wrappedHandler);
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

    public abstract class HandlerWrapper : IHandlerWrapper, IAckRawClientEndpoint
    {
        private readonly IAckRawWrapperServerLogic _logic;

        private volatile IAckRawServerHandler _wrappedHandler = null!;

        private volatile IAckRawClientEndpoint? _wrappedEndpoint;

        private readonly object mSendCallSerializer = new ();

        protected HandlerWrapper(IAckRawWrapperServerLogic logic)
        {
            _logic = logic;
        }

        public void Init(IAckRawServerHandler wrappedHandler)
        {
            _wrappedHandler = wrappedHandler;
        }

        public bool CheckAckData(UnionDataList ackData)
        {
            return _logic.ProcessAckData(ackData);
        }

        void IAckRawServerHandler.GetAckResponse(UnionDataList ackResponse)
        {
            _wrappedHandler.GetAckResponse(ackResponse);
        }

        void IAckRawServerHandler.OnConnected(IAckRawClientEndpoint endPoint)
        {
            _wrappedEndpoint = endPoint;
            _wrappedHandler.OnConnected(this);
            _logic.OnConnected();
        }

        void IRawBaseHandler.OnDisconnected(StopReason reason)
        {
            _logic.OnDisconnected();
            _wrappedHandler.OnDisconnected(reason);
            _wrappedEndpoint = null;
        }

        void IRawBaseHandler.OnReceived(UnionDataList receivedBuffer)
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

        SendResult IAckRawBaseEndpoint.Send(UnionDataList bufferToSend)
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

        public void Setup(IMemoryRental memory, ILogger logger)
        {
            _wrappedHandler.Setup(memory, logger);
        }
    }
}
