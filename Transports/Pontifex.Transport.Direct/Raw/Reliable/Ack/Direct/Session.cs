using System;
using Actuarius.Collections;
using Actuarius.Concurrent;
using Actuarius.Memory;
using Pontifex.StopReasons;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Raw.Reliable.Ack.Direct
{
    internal class Session : IServerDirectCtl//, IRawAckClientEndpoint
    {
        private readonly IRawReliableAckServerHandler _handler;
        private readonly IMemoryRental _memory;
        
        private DirectTransport _transport = null!;

        private readonly object _locker = new();
        private CycleQueue<UnionDataList>? _inAckQueue;

        public Session(IRawReliableAckServerHandler handler, IMemoryRental memory)
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
            _handler.FillAckResponse(ackResponse);
            ackResponse.PutFirst(new UnionData(DirectInfo.AckOKResponse));

            lock (_locker)
            {
                _inAckQueue = new();
                _transport.ServerSide.Send(ackResponse);

                try
                {
                    _handler.OnConnected(_transport.ServerSide);
                    var queue = _inAckQueue;
                    _inAckQueue = null;
                    while (queue.TryPop(out var data))
                    {
                        _handler.OnReceived(data);
                    }
                }
                catch (Exception ex)
                {
                    Log.wtf(ex);
                    _transport.Disconnect(new ExceptionFail("direct-server", ex));
                }
            }
        }

        void IAnyDirectCtl.OnReceived(UnionDataList buffer)
        {
            lock (_locker)
            {
                if (_inAckQueue != null)
                {
                    _inAckQueue.Put(buffer);
                }
                else
                {
                    _handler.OnReceived(buffer);
                }
            }
        }

        void IAnyDirectCtl.OnDisconnected(StopReason reason)
        {
            _handler.OnDisconnected(reason);
        }
    }
}