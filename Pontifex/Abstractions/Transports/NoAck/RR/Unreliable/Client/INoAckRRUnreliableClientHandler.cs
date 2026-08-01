using Pontifex.Utils;

namespace Pontifex.NoAck.RR.Unreliable
{
    public interface INoAckRRUnreliableClientHandler : IHandler
    {
        void Started(INoAckRRUnreliableServerEndpoint endpoint);

        void Received(UnionDataList message);

        void Stopped();
    }
}
