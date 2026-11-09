using Pontifex.Raw.Reliable;

namespace Pontifex.Tests.Transports;

/// <summary>
/// Black-box contract tests for <see cref="RawReliableTransport"/>. The class is a marker facade
/// over <see cref="Pontifex.Raw.RawTransport"/> and adds no behavior, so this test-set covers only its delta:
/// the reliable raw transport interface.
/// </summary>
[Category("Transports")]
public sealed class RawReliableTransportTests
{
    private static TestRawReliableTransport Create()
    {
        return new TestRawReliableTransport();
    }

    [Test]
    public void RawReliableTransport_ExposesReliableRawTransportContract()
    {
        var transport = Create();

        Assert.That(transport, Is.InstanceOf<IRawReliableTransport>());
    }
}
