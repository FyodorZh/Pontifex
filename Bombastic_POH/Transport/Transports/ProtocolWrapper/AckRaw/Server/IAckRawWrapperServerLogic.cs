using Pontifex.Utils;
using Shared.Buffer;

namespace Transport.Transports.ProtocolWrapper.AckRaw
{
    public interface IAckRawWrapperServerLogic
    {
        bool ProcessAckData(UnionDataList ackData);
        void OnConnected();
        void OnDisconnected();
        bool ProcessReceivedData(IMemoryBuffer receivedData);
        bool ProcessSentData(IMemoryBuffer sentData);
    }
}
