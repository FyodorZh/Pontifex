using Pontifex.Utils;

namespace Pontifex.NoAck.RR.Reliable
{
    public interface INoAckReliableRRServerHandler : IHandler
    {
        INoAckReliableRRClientSession OpenSession(IEndPoint client);
    }
}
