using Shared;
using Shared.Buffer;

namespace Transport.Transports.ProtocolWrapper.AckRaw
{
    public interface IAckRawWrapperServerLogic
    {
        ByteArraySegment ProcessAckData(ByteArraySegment data);
        void OnConnected();
        void OnDisconnected();
        bool ProcessReceivedData(IMemoryBuffer receivedData);
        bool ProcessSentData(IMemoryBuffer sentData);
    }
}
