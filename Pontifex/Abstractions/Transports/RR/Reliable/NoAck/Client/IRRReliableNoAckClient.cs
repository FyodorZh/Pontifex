namespace Pontifex.RR.Reliable.NoAck
{
    public interface IRRReliableNoAckClient : IRRNoAckClient
    {
        bool Init(IRRReliableNoAckClientHandler handler);
    }
}
