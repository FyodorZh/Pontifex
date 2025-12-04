using Actuarius.Memory;
using Pontifex.Utils;

namespace Transport.Transports.ProtocolWrapper.AckRaw
{
    public abstract class AckRawWrapperLogic : IAckRawWrapperLogic
    {
        protected ILogger Log { get; private set; } = global::Log.StaticLogger;
        protected IMemoryRental Memory { get; private set; } = MemoryRental.Shared;

        public void Setup(IMemoryRental memoryRental, ILogger logger)
        {
            Log = logger;
            Memory = memoryRental;
        }

        public abstract void OnConnected();
        public abstract void OnDisconnected();
        public abstract bool ProcessReceivedData(UnionDataList receivedData);
        public abstract bool ProcessSentData(UnionDataList sentData);
    }
}