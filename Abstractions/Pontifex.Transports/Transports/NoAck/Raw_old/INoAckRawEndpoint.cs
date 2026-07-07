namespace Pontifex.NoAck.Raw_old
{
    public interface INoAckRawEndpoint : IBaseEndpoint
    {
        int MessageMaxByteSize { get; }
    }
}