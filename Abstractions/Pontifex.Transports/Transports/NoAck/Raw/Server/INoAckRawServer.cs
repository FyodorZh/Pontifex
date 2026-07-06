namespace Pontifex.NoAck.Raw
{
    public interface INoAckRawServer : ITransport
    {
        bool Init(INoAckRawServerSideHandler handler);
    }
}