using Pontifex.Utils;
using Transport.Abstractions;

namespace Transport.Transports.ProtocolWrapper.AckRaw
{
    public interface IAckRawWrapperClientLogic
    {
        IControlProvider Controls { get; }

        void OnConnected();
        void OnDisconnected();
        void UpdateAckData(UnionDataList ackData);
        bool ProcessReceivedData(IMemoryBuffer receivedData);
        bool ProcessSentData(IMemoryBuffer sentData);
    }
}
