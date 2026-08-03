namespace Pontifex.RR.Unreliable.NoAck
{
    public interface IRRNoAckServerEndpoint
    {
        IEndPoint EndPoint { get; }
        int MessageMaxByteSize { get; }
    }
}
