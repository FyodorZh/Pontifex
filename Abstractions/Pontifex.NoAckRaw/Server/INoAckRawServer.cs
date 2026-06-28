namespace Pontifex.NoAckRaw
{
    public interface INoAckRawServer : ITransport
    {
        bool Init(INoAckRawServerSideHandler handler);
    }
}