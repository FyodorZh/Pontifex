namespace Pontifex.NoAck.RR.Reliable
{
    public interface INoAckReliableRRClient : INoAckRRClient
    {
        bool Init(INoAckReliableRRClientHandler handler);
    }
}
