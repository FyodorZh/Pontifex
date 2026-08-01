namespace Pontifex.NoAck.RR.Reliable
{
    public interface INoAckRRReliableServer : INoAckRRServer
    {
        bool Init(INoAckRRReliableServerHandler handler);
    }
}
