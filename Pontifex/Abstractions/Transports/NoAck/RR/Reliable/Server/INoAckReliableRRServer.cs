namespace Pontifex.NoAck.RR.Reliable
{
    public interface INoAckReliableRRServer : INoAckRRServer
    {
        bool Init(INoAckReliableRRServerHandler handler);
    }
}
