using Pontifex.Utils;

namespace Pontifex.NoAck.RR.Unreliable
{
    public interface INoAckUnreliableRRClientHandler : IHandler
    {
        void Started(INoAckUnreliableRRServerEndpoint endpoint);

        void Received(UnionDataList message);

        void Stopped();
    }
}
