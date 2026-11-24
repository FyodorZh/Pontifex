using System;
using Actuarius.Memory;
using Pontifex.Factory.Registration;
using Pontifex.Raw.Reliable.Direct;
using Pontifex.Tests.RR.RRReliableDirect.Harness;
using Scriba;

namespace Pontifex.Tests.Raw.Reliable.RawReliable;

/// <summary>
/// Pins the Direct RawReliable binding parameters: the full handshake-maximum range, the message/buffer
/// parameters, the positive handshake timeout, and the handshake-versus-message consistency check.
/// </summary>
[TestFixture]
public sealed class RawReliableDirectBindingParametersTests
{
    private static readonly ILogger Logger = new Logger([]);

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        Logger.Dispose();
    }

    [Test]
    public void HandshakeMaximum_AcceptsIntMaxValueWhenMessageMaximumAllowsIt()
    {
        var server = (RawReliableDirectServerTransport)Create(TransportRole.Server,
            context => context.WithInt64("handshake_max_byte_size", int.MaxValue)
                .WithInt64("message_max_size", int.MaxValue));

        Assert.Multiple(() =>
        {
            Assert.That(server.HandshakeMaxByteSize, Is.EqualTo(int.MaxValue));
            Assert.That(server.MessageMaxByteSize, Is.EqualTo(int.MaxValue));
        });
    }

    [Test]
    public void HandshakeMaximum_RejectsIntMaxValuePlusOne()
    {
        Assert.That(
            () => Create(TransportRole.Server,
                context => context.WithInt64("handshake_max_byte_size", (long)int.MaxValue + 1)),
            Throws.InstanceOf<Exception>());
    }

    [Test]
    public void HandshakeMaximum_AboveTheDefaultMessageMaximum_IsRejected()
    {
        Assert.That(
            () => Create(TransportRole.Server,
                context => context.WithInt64("handshake_max_byte_size",
                    DirectInfo.DefaultMessageMaxSize + 1)),
            Throws.TypeOf<ArgumentException>());
    }

    [Test]
    public void HandshakeMaximum_AboveAConfiguredMessageMaximum_IsRejected()
    {
        Assert.That(
            () => Create(TransportRole.Server,
                context => context.WithInt64("message_max_size", 1024)
                    .WithInt64("handshake_max_byte_size", 2048)),
            Throws.TypeOf<ArgumentException>());
    }

    [Test]
    public void Create_AppliesTheMessageBufferAndTimeoutParameters()
    {
        var client = (RawReliableDirectClientTransport)Create(TransportRole.Client,
            context => context.WithInt64("message_max_size", 2048)
                .WithInt64("buffer_capacity_bytes", 4096)
                .WithInt64("handshake_timeout", 5));

        Assert.Multiple(() =>
        {
            Assert.That(client.MessageMaxByteSize, Is.EqualTo(2048));
            Assert.That(client.BufferCapacityBytesForTests, Is.EqualTo(4096));
            Assert.That(client.HandshakeTimeoutForTests, Is.EqualTo(TimeSpan.FromSeconds(5)));
        });
    }

    [Test]
    public void Create_WithoutTheNewParameters_UsesTheDocumentedDefaults()
    {
        var client = (RawReliableDirectClientTransport)Create(TransportRole.Client);
        var server = (RawReliableDirectServerTransport)Create(TransportRole.Server);

        Assert.Multiple(() =>
        {
            Assert.That(client.MessageMaxByteSize, Is.EqualTo(DirectInfo.DefaultMessageMaxSize));
            Assert.That(client.BufferCapacityBytesForTests, Is.EqualTo(DirectInfo.DefaultBufferCapacity));
            Assert.That(client.HandshakeTimeoutForTests, Is.EqualTo(TimeSpan.FromSeconds(10)));
            Assert.That(server.HandshakeTimeout, Is.EqualTo(TimeSpan.FromSeconds(10)));
        });
    }

    [Test]
    public void HandshakeTimeout_Zero_IsRejectedByTheDeclaredRange()
    {
        Assert.That(
            () => Create(TransportRole.Client,
                context => context.WithInt64("handshake_timeout", 0)),
            Throws.InstanceOf<ArgumentException>());
    }

    [Test]
    public void PublicClientConstructor_RejectsANonPositiveOrInfiniteDeadline()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                () => new RawReliableDirectClientTransport("server", Logger, MemoryRental.Shared,
                    handshakeTimeout: TimeSpan.Zero),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(
                () => new RawReliableDirectClientTransport("server", Logger, MemoryRental.Shared,
                    handshakeTimeout: System.Threading.Timeout.InfiniteTimeSpan),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        });
    }

    [Test]
    public void PublicServerConstructor_RejectsANonPositiveOrInfiniteDeadline()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                () => new RawReliableDirectServerTransport("server", Logger, MemoryRental.Shared,
                    handshakeTimeout: TimeSpan.Zero),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(
                () => new RawReliableDirectServerTransport("server", Logger, MemoryRental.Shared,
                    handshakeTimeout: System.Threading.Timeout.InfiniteTimeSpan),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        });
    }

    private static ITransport Create(TransportRole role, Action<TestBuildContext>? configure = null)
    {
        var context = new TestBuildContext(role, Logger, MemoryRental.Shared)
            .WithString("id", "reliable-params-" + Guid.NewGuid().ToString("N"));
        configure?.Invoke(context);
        return RawReliableDirectBinding.CreateRegistration().Create(context);
    }
}
