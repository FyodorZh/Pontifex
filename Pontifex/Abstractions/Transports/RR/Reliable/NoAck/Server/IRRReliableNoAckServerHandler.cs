using Pontifex.Utils;

namespace Pontifex.RR.Reliable.NoAck
{
    public interface IRRReliableNoAckServerHandler : IHandler
    {
        IRRReliableNoAckClientSession OpenSession(IEndPoint client);
    }
}
