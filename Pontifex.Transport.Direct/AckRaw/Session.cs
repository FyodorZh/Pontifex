using System;
using Actuarius.Memory;
using Pontifex.Utils;
using Scriba;
using Transport.Abstractions.Handlers.Server;
using Transport.StopReasons;

namespace Transport.Transports.Direct
{
    internal class Session : IServerDirectCtl//, IAckRawClientEndpoint
    {
        private readonly IAckRawServerHandler _handler;
        private readonly IMemoryRental _memory;
        
        private DirectTransport _transport = null!;

        public Session(IAckRawServerHandler handler, IMemoryRental memory)
        {
            _handler = handler;
            _memory = memory;
        }

        void IServerDirectCtl.Init(DirectTransport transport)
        {
            _transport = transport;
        }

        void IServerDirectCtl.OnClientPrepared()
        {
            UnionDataList ackResponse = _memory.CollectablePool.Acquire<UnionDataList>();
            _handler.GetAckResponse(ackResponse);
            ackResponse.PutFirst(new UnionData(DirectInfo.AckOKResponse));

            _transport.ServerSide.Send(ackResponse);

            try
            {
                _handler.OnConnected(_transport.ServerSide);
            }
            catch (Exception ex)
            {
                Log.wtf(ex);
                _transport.Disconnect(new ExceptionFail("direct-server", ex));
            }
        }

        void IAnyDirectCtl.OnReceived(UnionDataList buffer)
        {
            _handler.OnReceived(buffer);
        }

        void IAnyDirectCtl.OnDisconnected(StopReason reason)
        {
            _handler.OnDisconnected(reason);
        }
    }
}