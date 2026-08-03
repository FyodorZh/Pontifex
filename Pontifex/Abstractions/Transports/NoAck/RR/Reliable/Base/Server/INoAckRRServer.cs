namespace Pontifex.NoAck.RR.Reliable
{
    public interface INoAckRRServer : ITransport
    {
        int MessageMaxByteSize { get; }
    }
}
