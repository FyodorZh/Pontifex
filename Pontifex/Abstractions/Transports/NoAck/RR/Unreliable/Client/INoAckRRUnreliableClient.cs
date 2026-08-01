namespace Pontifex.NoAck.RR.Unreliable
{
    public interface INoAckRRUnreliableClient : INoAckRRClient
    {
        bool Init(INoAckRRUnreliableClientHandler handler);

        int MessageMaxByteSize { get; }
    }
}
