namespace Pontifex.Raw.Reliable.Ack
{
    /// <summary>
    /// Client-side view of an ACK raw endpoint.
    /// Provides send/disconnect operations scoped to the client's connection.
    /// Implemented by the transport system.
    /// </summary>
    public interface IRawReliableAckClientSideEndpoint : IRawReliableAckBaseEndpoint
    {
    }
}