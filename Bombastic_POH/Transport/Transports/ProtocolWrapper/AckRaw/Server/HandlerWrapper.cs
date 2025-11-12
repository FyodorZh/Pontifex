using Shared;
using Transport.Abstractions;
using Transport.Abstractions.Endpoints;
using Transport.Abstractions.Endpoints.Server;
using Transport.Abstractions.Handlers;
using Transport.Abstractions.Handlers.Server;
using Shared.Buffer;
using Transport.Endpoints;

namespace Transport.Transports.ProtocolWrapper.AckRaw
{
    public interface IHandlerWrapper : IAckRawServerHandler
    {
        void Init(IAckRawServerHandler wrappedHandler);
        ByteArraySegment CheckAckData(ByteArraySegment ackData);
    }


    public class HandlerWrapper<TLogic> : HandlerWrapper
        where TLogic : IAckRawWrapperServerLogic
    {
        public HandlerWrapper(IConstructor<TLogic> constructor)
            : base(constructor.Construct())
        {
        }
    }

    public abstract class HandlerWrapper : IHandlerWrapper, IAckRawClientEndpoint
    {
        private readonly IAckRawWrapperServerLogic mLogic;

        private volatile IAckRawServerHandler mWrappedHandler;

        private volatile IAckRawClientEndpoint mWrappedEndpoint;

        private readonly object mSendCallSerializer = new object();

        protected HandlerWrapper(IAckRawWrapperServerLogic logic)
        {
            mLogic = logic;
        }

        public void Init(IAckRawServerHandler wrappedHandler)
        {
            mWrappedHandler = wrappedHandler;
        }

        public ByteArraySegment CheckAckData(ByteArraySegment ackData)
        {
            return mLogic.ProcessAckData(ackData);
        }

        byte[] IAckRawServerHandler.GetAckResponse()
        {
            return mWrappedHandler.GetAckResponse();
        }

        void IAckRawServerHandler.OnConnected(IAckRawClientEndpoint endPoint)
        {
            mWrappedEndpoint = endPoint;
            mWrappedHandler.OnConnected(this);
            mLogic.OnConnected();
        }

        void IRawBaseHandler.OnDisconnected(StopReason reason)
        {
            mLogic.OnDisconnected();
            mWrappedHandler.OnDisconnected(reason);
            mWrappedEndpoint = null;
        }

        void IRawBaseHandler.OnReceived(IMemoryBufferHolder receivedBuffer)
        {
            using (var bufferAccessor = receivedBuffer.ExposeAccessorOnce())
            {
                if (mLogic.ProcessReceivedData(bufferAccessor.Buffer))
                {
                    mWrappedHandler.OnReceived(bufferAccessor.Acquire());
                    return; // OK
                }

                // Failed
                var endpoint = mWrappedEndpoint;
                if (endpoint != null)
                {
                    endpoint.Disconnect(new StopReasons.TextFail("???", "Failed to process received data"));
                }
            }
        }

        IEndPoint IAckRawBaseEndpoint.RemoteEndPoint
        {
            get
            {
                var endpoint = mWrappedEndpoint;
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
                var endpoint = mWrappedEndpoint;
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
                var endpoint = mWrappedEndpoint;
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
                    var endpoint = mWrappedEndpoint;
                    if (endpoint != null)
                    {
                        if (mLogic.ProcessSentData(bufferAccessor.Buffer))
                        {
                            return endpoint.Send(bufferAccessor.Acquire());
                        }
                    }
                    return SendResult.Error;
                }
            }
        }

        bool IAckRawBaseEndpoint.Disconnect(StopReason reason)
        {
            var endpoint = mWrappedEndpoint;
            if (endpoint != null)
            {
                return endpoint.Disconnect(reason);
            }
            return false;
        }
    }
}
