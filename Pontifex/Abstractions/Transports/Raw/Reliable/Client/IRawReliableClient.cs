namespace Pontifex.Raw.Reliable
{
    /// <summary>
    /// Marker interface for a reliable (e.g. TCP) raw client transport.
    /// Reliable transport guarantees in-order, lossless message delivery.
    /// </summary>
    public interface IRawReliableClient
        : IRawReliableClientTransport<IRawReliableClientHandler>
    {
    }
}
