using Transport.Abstractions;
using Shared.Buffer;

namespace Transport.Transports.ProtocolWrapper.AckRaw
{
    public interface IAckRawWrapperClientLogic
    {
        IControlProvider Controls { get; }

        void OnConnected();
        void OnDisconnected();
        byte[] UpdateAckData(byte[] originalAck);
        bool ProcessReceivedData(IMemoryBuffer receivedData);
        bool ProcessSentData(IMemoryBuffer sentData);
    }
}
