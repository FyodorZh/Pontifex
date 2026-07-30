using Pontifex.Utils;

namespace Pontifex.NoAck.Raw.Reliable
{
    public interface INoAckRawReliableServerHandler : IHandler
    {
        void OnConnected(INoAckRawReliableServerSideEndpoint endpoint);

        void OnReceived(UnionDataList receivedBuffer);

        void OnDisconnected(StopReason reason);
    }
}
