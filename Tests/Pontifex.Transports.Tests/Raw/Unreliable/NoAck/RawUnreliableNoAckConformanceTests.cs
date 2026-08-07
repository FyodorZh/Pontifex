using Pontifex.Raw.Unreliable.NoAck;

namespace Pontifex.Tests.Raw.Unreliable.NoAck;

public abstract class RawUnreliableNoAckConformanceTests : RawUnreliableConformanceTests<IRawUnreliableNoAckServer>
{
}

[TestFixture]
public sealed class DirectRawUnreliableNoAckConformanceTests : RawUnreliableNoAckConformanceTests
{
    protected override IRawUnreliableConformanceAdapter<IRawUnreliableNoAckServer> CreateAdapter()
    {
        return new DirectRawUnreliableNoAckConformanceAdapter();
    }
}
