using Pontifex.Utils;

namespace Pontifex.NoAck.Raw_old
{
    public interface INoAckRawServerSideHandler : IHandler
    {
        void OnStarted(INoAckRawServerSideEndpoint endpoint);
        void OnStopped(StopReason reason);
        void OnReceived(IEndPoint sender, UnionDataList message);
    }
}