namespace Pontifex.NoAck.RR
{
    public interface INoAckRRServerEndpoint
    {
        IEndPoint EndPoint { get; }
        int MessageMaxByteSize { get; }
    }
}
