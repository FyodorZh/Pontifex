using Pontifex.Raw.Unreliable.Ack;

namespace Pontifex.Tests.Raw.Unreliable.Ack;

/// <summary>
/// Creates implementation-specific RawUnreliableAck topologies for the
/// carrier-independent conformance suite.
/// </summary>
public interface IRawUnreliableAckConformanceAdapter : IRawUnreliableConformanceAdapter<IRawUnreliableAckServer>
{
}
