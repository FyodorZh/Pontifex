namespace Pontifex.Tests.Raw.Reliable.Ack;

[TestFixture]
public sealed class DirectRawReliableAckConformanceAdapterTests : RawReliableAckConformanceTests
{
    protected override IRawReliableAckConformanceAdapter CreateAdapter()
    {
        return new DirectRawReliableAckConformanceAdapter();
    }
}
