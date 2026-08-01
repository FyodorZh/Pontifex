namespace Pontifex.NoAck.RR.Reliable
{
    public interface INoAckRRReliableClientHandler : IHandler
    {
        void Started(INoAckRRReliableServerEndpoint endpoint);
        void Stopped();
    }
}
