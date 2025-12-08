using Actuarius.Memory;
using Pontifex.Utils;
using Scriba;

namespace Transport.Transports.ProtocolWrapper.AckRaw
{
    public interface IAckRawWrapperLogic
    {
        void Setup(IMemoryRental memoryRental, ILogger logger);
        void OnConnected();
        void OnDisconnected();
        bool ProcessReceivedData(UnionDataList receivedData);
        bool ProcessSentData(UnionDataList sentData);
    }
}