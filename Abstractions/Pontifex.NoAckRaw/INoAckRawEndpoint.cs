namespace Pontifex.NoAckRaw
{
    public interface INoAckRawEndpoint
    {
        int MessageMaxByteSize { get; }
    }
}