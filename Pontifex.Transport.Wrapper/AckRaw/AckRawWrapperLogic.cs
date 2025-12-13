using Actuarius.Memory;
using Pontifex.Utils;
using Scriba;

namespace Transport.Transports.ProtocolWrapper.AckRaw
{
    public abstract class AckRawWrapperLogic : IAckRawWrapperLogic
    {
        protected ILogger Log { get; }
        protected IMemoryRental Memory { get; }

        protected AckRawWrapperLogic(ILogger logger, IMemoryRental memoryRental)
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