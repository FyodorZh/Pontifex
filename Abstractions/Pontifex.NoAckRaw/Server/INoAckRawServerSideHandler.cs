using Pontifex.Utils;

namespace Pontifex.NoAckRaw
{
    public interface INoAckRawServerSideHandler : IHandler
    {
        void OnStarted(INoAckRawServerSideEndpoint endpoint);
        void OnStopped();
        void OnReceived(IEndPoint sender, UnionDataList message);
    }
}