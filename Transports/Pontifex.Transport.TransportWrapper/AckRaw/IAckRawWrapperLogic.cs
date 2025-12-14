using Pontifex.Utils;

namespace Pontifex.Transports.TransportWrapper.AckRaw
{
    public interface IAckRawWrapperLogic
    {
        void OnConnected();
        void OnDisconnected();
        bool ProcessReceivedData(UnionDataList receivedData);
        bool ProcessSentData(UnionDataList sentData);
    }
}