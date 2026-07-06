namespace Pontifex.Ack.Raw
{
    /// <summary>
    /// Server-side view of an ACK raw endpoint.
    /// Provides send/disconnect operations per client session.
    /// Implemented by the transport system.
    /// </summary>
    public interface IAckRawServerSideEndpoint : IAckRawBaseEndpoint
    {
    }
}