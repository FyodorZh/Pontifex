namespace Pontifex.NoAck.RR.Unreliable
{
    public interface INoAckRRServer : ITransport
    {
        int MessageMaxByteSize { get; }
    }
}
