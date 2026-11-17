namespace Pontifex.Raw.Reliable.NoAck
{
    /// <summary>
    /// Marker interface for a reliable (e.g. TCP) no-acknowledgement raw client transport.
    /// Reliable transport guarantees in-order, lossless message delivery.
    /// </summary>
    public interface IRawReliableNoAckClient
        : IRawReliableClientTransport<IRawReliableNoAckClientHandler>
    {
    }
}
