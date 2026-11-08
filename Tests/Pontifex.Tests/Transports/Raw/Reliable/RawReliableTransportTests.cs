using Actuarius.Memory;
using Pontifex.Raw;
using Pontifex.Raw.Reliable;

namespace Pontifex.Tests.Transports;

/// <summary>
/// Black-box contract tests for <see cref="RawReliableTransport"/>. The class is a marker facade
/// over <see cref="RawTransport"/> and adds no behavior, so this test-set covers only its delta:
/// constructor forwarding and the reliable raw transport interface.
/// </summary>
[Category("Transports")]
public sealed class RawReliableTransportTests
{
    private static TestRawReliableTransport Create(
        string name = "TestRawReliableTransport",
        IMemoryRental? memory = null)
    {
        return new TestRawReliableTransport(name, memory: memory);
    }

    [Test]
    public void Constructor_ForwardsBaseState()
    {
        IMemoryRental memory = MemoryRental.Shared;

        var transport = new TestRawReliableTransport(
            "MyRawReliableTransport",
            memory: memory);

        Assert.That(transport.Name, Is.EqualTo("MyRawReliableTransport"));
        Assert.That(transport.Memory, Is.SameAs(memory));
        Assert.That(transport.Log, Is.Not.Null);
    }

    [Test]
    public void RawReliableTransport_ExposesReliableRawTransportContract()
    {
        var transport = Create();

        Assert.That(transport, Is.InstanceOf<IRawReliableTransport>());
        Assert.That(transport, Is.InstanceOf<IRawTransport>());
        Assert.That(transport, Is.InstanceOf<ITransport>());
    }
}
