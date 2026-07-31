namespace Pontifex.NoAck.RR
{
    public interface INoAckRRServer : ITransport
    {
        int MessageMaxByteSize { get; }
    }
}
