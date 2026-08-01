namespace Pontifex.NoAck.RR.Reliable
{
    public interface INoAckRRReliableClient : INoAckRRClient
    {
        bool Init(INoAckRRReliableClientHandler handler);
    }
}
