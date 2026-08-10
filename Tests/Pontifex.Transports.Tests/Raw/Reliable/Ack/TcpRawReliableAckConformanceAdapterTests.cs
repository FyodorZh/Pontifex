namespace Pontifex.Tests.Raw.Reliable.Ack;

[TestFixture]
public sealed class TcpRawReliableAckConformanceAdapterTests : RawReliableAckConformanceTests
{
    protected override IRawReliableAckConformanceAdapter CreateAdapter()
    {
        return new TcpRawReliableAckConformanceAdapter();
    }
}
