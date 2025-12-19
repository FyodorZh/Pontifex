using Pontifex.Utils;

namespace Pontifex.Protocols
{
    public interface IAckRawWrapperLogic
    {
        void OnConnected();
        void OnDisconnected();
        bool ProcessReceivedData(UnionDataList receivedData);
        bool ProcessSentData(UnionDataList sentData);
    }
}