namespace Pontifex.RR.Unreliable.NoAck
{
    public interface IRRUnreliableNoAckClient : IRRNoAckClient
    {
        bool Init(IRRUnreliableNoAckClientHandler handler);

        int MessageMaxByteSize { get; }
    }
}
