namespace Pontifex.NoAck.RR.Reliable
{
    public interface INoAckReliableRRClientHandler : IHandler
    {
        void Started(INoAckReliableRRServerEndpoint endpoint);
        void Stopped();
    }
}
