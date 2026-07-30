using Pontifex.Utils;

namespace Pontifex.NoAck.Raw.Reliable
{
    public interface INoAckRawReliableClientHandler : IHandler
    {
        void OnConnected(INoAckRawReliableClientSideEndpoint endpoint);

        void OnReceived(UnionDataList receivedBuffer);

        void OnDisconnected(StopReason reason);

        void OnStopped(StopReason reason);
    }
}
