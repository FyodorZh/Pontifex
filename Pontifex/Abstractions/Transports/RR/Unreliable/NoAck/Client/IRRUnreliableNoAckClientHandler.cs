using Pontifex.Utils;

namespace Pontifex.RR.Unreliable.NoAck
{
    public interface IRRUnreliableNoAckClientHandler : IHandler
    {
        void Started(IRRUnreliableNoAckServerEndpoint endpoint);

        void Received(UnionDataList message);

        void Stopped();
    }
}
