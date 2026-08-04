namespace Pontifex.RR.Reliable.NoAck
{
    public interface IRRReliableNoAckServer : IRRNoAckServer
    {
        bool Init(IRRReliableNoAckServerHandler handler);
    }
}
