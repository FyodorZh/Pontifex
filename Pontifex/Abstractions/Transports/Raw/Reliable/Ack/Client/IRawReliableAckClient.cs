namespace Pontifex.Raw.Reliable.Ack
{
    /// <summary>
    /// Marker interface for a reliable (e.g. TCP) ACK raw client transport.
    /// Reliable transport guarantees in-order, lossless message delivery.
    /// </summary>
    public interface IRawReliableAckClient : IRawReliableClientTransport<IRawReliableAckClientHandler>
    {        
    }
}