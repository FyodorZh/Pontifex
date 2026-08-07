using Pontifex.Raw.Reliable;

namespace Pontifex.Tests.Raw.Reliable.Ack;

/// <summary>
/// Minimal tracking surface a test handler needs from its fixture: register
/// every endpoint handed to OnConnected so fixture disposal can reset its gates.
/// </summary>
public interface IRawReliableAckEndpointTracker
{
    void TrackEndpoint(IRawReliableEndpoint endpoint);
}
