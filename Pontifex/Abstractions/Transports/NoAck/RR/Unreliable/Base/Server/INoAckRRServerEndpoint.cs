namespace Pontifex.NoAck.RR.Unreliable
{
    public interface INoAckRRServerEndpoint
    {
        IEndPoint EndPoint { get; }
        int MessageMaxByteSize { get; }
    }
}
