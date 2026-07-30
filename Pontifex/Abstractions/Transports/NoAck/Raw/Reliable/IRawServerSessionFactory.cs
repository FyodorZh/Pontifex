namespace Pontifex.NoAck.Raw.Reliable
{
    public interface IRawServerSessionFactory<out THandler>
        where THandler : INoAckRawReliableServerHandler
    {
        THandler CreateSession();
    }
}
