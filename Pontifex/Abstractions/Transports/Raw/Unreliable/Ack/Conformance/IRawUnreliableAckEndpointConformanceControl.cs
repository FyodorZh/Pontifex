namespace Pontifex.Raw.Unreliable.Ack
{
    /// <summary>
    /// Test-only control for a single IRawUnreliableEndpoint, obtained via
    /// IRawUnreliableEndpoint.GetControls after the endpoint is received in OnStarted.
    /// </summary>
    public interface IRawUnreliableAckEndpointConformanceControl : IRawUnreliableEndpointConformanceControl
    {
    }
}
