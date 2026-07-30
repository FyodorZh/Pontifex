namespace Pontifex.NoAck.Raw.Reliable
{
    public interface INoAckRawReliableServer : ITransport
    {
        int MessageMaxByteSize { get; }

        bool Init(IRawServerSessionFactory<INoAckRawReliableServerHandler> sessionFactory);
    }
}
