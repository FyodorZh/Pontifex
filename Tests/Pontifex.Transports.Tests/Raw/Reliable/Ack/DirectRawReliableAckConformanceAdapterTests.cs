namespace Pontifex.Tests.Raw.Reliable.Ack;

[TestFixture]
[Ignore("RawReliableAck conformance implementation is incomplete; 28 tests currently fail.")]
public sealed class DirectRawReliableAckConformanceAdapterTests : RawReliableAckConformanceTests
{
    protected override IRawReliableAckConformanceAdapter CreateAdapter()
    {
        return new DirectRawReliableAckConformanceAdapter();
    }
}
