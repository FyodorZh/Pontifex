using System;
using Shared.Buffer;
using Transport.Abstractions.Endpoints.Server;
using Transport.Abstractions.Handlers.Server;
using Transport.StopReasons;

namespace Transport.Transports.Direct
{
    internal class Session : IServerDirectCtl//, IAckRawClientEndpoint
    {
        private readonly IAckRawServerHandler mHandler;
        private DirectTransport mTransport;

        public Session(IAckRawServerHandler handler)
        {
            mHandler = handler;
        }

        void IServerDirectCtl.Init(DirectTransport transport)
        {
            mTransport = transport;
        }

        void IServerDirectCtl.OnClientPrepared()
        {
            byte[] ackResponse = AckUtils.AppendPrefix(mHandler.GetAckResponse(), DirectInfo.AckOKResponse);

            mTransport.ServerSide.Send(ConcurrentUsageMemoryBufferPool.Instance.AllocateAndPush(ackResponse));

            try
            {
                mHandler.OnConnected((IAckRawClientEndpoint)mTransport.ServerSide);
            }
            catch (Exception ex)
            {
                Log.wtf(ex);
                mTransport.Disconnect(new ExceptionFail("direct-server", ex));
            }
        }

        void IAnyDirectCtl.OnReceived(IMemoryBufferHolder buffer)
        {
            mHandler.OnReceived(buffer);
        }

        void IAnyDirectCtl.OnDisconnected(StopReason reason)
        {
            mHandler.OnDisconnected(reason);
        }
    }
}