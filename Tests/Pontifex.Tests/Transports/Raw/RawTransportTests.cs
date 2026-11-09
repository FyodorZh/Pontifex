using Actuarius.Memory;
using Pontifex.Raw;

namespace Pontifex.Tests.Transports;

/// <summary>
/// Black-box contract tests for <see cref="RawTransport"/>. The class is a thin typed facade over
/// <see cref="AnyTransport"/>, so this test-set covers only its delta: constructor forwarding, the
/// derived message-size value, and the raw transport interface. The inherited lifecycle is covered
/// once by <see cref="AnyTransportTests"/> and is not repeated here; the constructor test verifies
/// only that the forwarded arguments reach the base state.
/// </summary>
[Category("Transports")]
public sealed class RawTransportTests
{
    private static TestRawTransport Create(
        string name = "TestRawTransport",
        int messageMaxByteSize = 1024,
        IMemoryRental? memory = null)
    {
        return new TestRawTransport(name, messageMaxByteSize, memory: memory);
    }

    [Test]
    public void Constructor_ForwardsBaseState()
    {
        IMemoryRental memory = MemoryRental.Shared;

        var transport = new TestRawTransport(
            "MyRawTransport",
            messageMaxByteSize: 2048,
            memory: memory);

        Assert.That(transport.Name, Is.EqualTo("MyRawTransport"));
        Assert.That(transport.Memory, Is.SameAs(memory));
        Assert.That(transport.Log, Is.Not.Null);
    }

    [TestCase(1)]
    [TestCase(1024)]
    [TestCase(65536)]
    public void MessageMaxByteSize_ReflectsDerivedImplementation(int messageMaxByteSize)
    {
        var transport = Create(messageMaxByteSize: messageMaxByteSize);

        Assert.That(transport.MessageMaxByteSize, Is.EqualTo(messageMaxByteSize));
    }

    [Test]
    public void RawTransport_IsAssignableToIRawTransport()
    {
        var transport = Create();

        Assert.That(transport, Is.InstanceOf<IRawTransport>());
        Assert.That(transport, Is.InstanceOf<ITransport>());
    }

    [Test]
    public void IRawTransport_ExposesMessageMaxByteSize()
    {
        IRawTransport transport = Create(messageMaxByteSize: 4096);

        Assert.That(transport.MessageMaxByteSize, Is.EqualTo(4096));
    }
}
