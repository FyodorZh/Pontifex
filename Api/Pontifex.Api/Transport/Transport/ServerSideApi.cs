using System;
using Actuarius.Memory;
using Pontifex.Raw.Reliable.Ack;
using Pontifex.Utils;

namespace Pontifex.Api
{
    public class ServerSideApiFactory<TApi> : IRawReliableAckServerAcknowledger<IRawReliableAckServerHandler>
        where TApi : IApiRoot
    {
        private readonly Func<UnionDataList, ServerSideApiInstance<TApi>?> _instanceFactory;
        private readonly IMemoryRental _memory;

        public ServerSideApiFactory(Func<UnionDataList, ServerSideApiInstance<TApi>?> instanceFactory, IMemoryRental? memory = null)
        {
            _instanceFactory = instanceFactory;
            _memory = memory ?? MemoryRental.Shared;
        }
        
        public (IRawReliableAckServerHandler handler, UnionDataList ackResponse)? TryAck(IEndPoint remote, UnionDataList ackData)
        {
            using var disposer = ackData.AsDisposable();
            if (ackData.TryPopFirst(out long apiHash) && apiHash == 777)
            {
                ServerSideApiInstance<TApi>? instance = _instanceFactory.Invoke(ackData);
                if (instance != null)
                {
                    UnionDataList ackResponse = _memory.CollectablePool.Acquire<UnionDataList>();
                    ackResponse.PutFirst((long)7777);
                    return (instance, ackResponse);
                }
            }

            return null;
        }
    }
}