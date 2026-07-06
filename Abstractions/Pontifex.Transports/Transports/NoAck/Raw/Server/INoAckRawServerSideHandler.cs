using Pontifex.Utils;

namespace Pontifex.NoAck.Raw
{
    public interface INoAckRawServerSideHandler : IHandler
    {
        void OnStarted(INoAckRawServerSideEndpoint endpoint);
        void OnStopped(StopReason reason);
        void OnReceived(IEndPoint sender, UnionDataList message);
    }
}