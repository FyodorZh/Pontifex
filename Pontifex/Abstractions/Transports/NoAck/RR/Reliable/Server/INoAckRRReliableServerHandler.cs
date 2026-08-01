using Pontifex.Utils;

namespace Pontifex.NoAck.RR.Reliable
{
    public interface INoAckRRReliableServerHandler : IHandler
    {
        INoAckRRReliableClientSession OpenSession(IEndPoint client);
    }
}
