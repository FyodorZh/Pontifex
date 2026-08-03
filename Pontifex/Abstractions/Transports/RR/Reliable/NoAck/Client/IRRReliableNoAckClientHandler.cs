namespace Pontifex.RR.Reliable.NoAck
{
    public interface IRRReliableNoAckClientHandler : IHandler
    {
        void Started(IRRReliableNoAckServerEndpoint endpoint);
        void Stopped();
    }
}
