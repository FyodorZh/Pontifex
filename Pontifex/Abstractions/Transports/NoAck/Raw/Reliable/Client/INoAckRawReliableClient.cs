namespace Pontifex.NoAck.Raw.Reliable
{
    public interface INoAckRawReliableClient : ITransport
    {
        int MessageMaxByteSize { get; }

        bool Init(INoAckRawReliableClientHandler handler);
    }
}
