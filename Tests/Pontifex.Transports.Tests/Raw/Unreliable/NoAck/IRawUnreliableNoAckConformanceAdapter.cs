using Pontifex.Raw.Unreliable.NoAck;

namespace Pontifex.Tests.Raw.Unreliable.NoAck;

/// <summary>
/// Creates implementation-specific RawUnreliableNoAck topologies for the
/// carrier-independent conformance suite.
/// </summary>
public interface IRawUnreliableNoAckConformanceAdapter : IRawUnreliableConformanceAdapter<IRawUnreliableNoAckServer>
{
}
