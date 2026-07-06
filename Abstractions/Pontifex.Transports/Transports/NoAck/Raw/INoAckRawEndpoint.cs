namespace Pontifex.NoAck.Raw
{
    public interface INoAckRawEndpoint
    {
        int MessageMaxByteSize { get; }
    }
}