namespace Pontifex.NoAck.Raw.Unreliable.Tests
{
    public class NoAckRawUnreliableDirectAdapter : INoAckRawUnreliableConformanceTestAdapter
    {
        public string ImplementationName => "NoAckRawUnreliable.Direct";

        public INoAckRawUnreliableConformanceScope CreateScope() => new NoAckRawUnreliableDirectScope();
    }
}