namespace Pontifex.RR.Reliable.NoAck
{
    public interface IRRNoAckServerEndpoint
    {
        IEndPoint EndPoint { get; }
        int MessageMaxByteSize { get; }
    }
}
