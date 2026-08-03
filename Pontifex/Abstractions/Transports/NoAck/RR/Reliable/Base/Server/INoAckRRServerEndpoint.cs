namespace Pontifex.NoAck.RR.Reliable
{
    public interface INoAckRRServerEndpoint
    {
        IEndPoint EndPoint { get; }
        int MessageMaxByteSize { get; }
    }
}
