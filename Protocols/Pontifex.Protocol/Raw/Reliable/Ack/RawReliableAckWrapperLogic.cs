using System;
using System.Collections.Generic;
using Actuarius.Memory;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Raw.Reliable.Ack.Protocols
{
    public abstract class RawReliableAckWrapperLogic : IRawReliableAckWrapperLogic
    {
        protected ILogger Log { get; }
        protected IMemoryRental Memory { get; }

        protected RawReliableAckWrapperLogic(ILogger logger, IMemoryRental memoryRental)
        {
            Log = logger;
            Memory = memoryRental;
        }

        public abstract void OnConnected();
        public abstract void OnDisconnected();
        public abstract bool ProcessReceivedData(UnionDataList receivedData);
        public abstract bool ProcessSentData(UnionDataList sentData);
        public abstract void GetControls(List<IControl> dst, Predicate<IControl>? predicate = null);
    }
}