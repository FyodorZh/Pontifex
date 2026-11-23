using System;
using Actuarius.Memory;
using Pontifex.Factory.Registration;
using Pontifex.RR.Direct;
using Pontifex.RR.Reliable;
using Pontifex.RR.Reliable.Direct;
using Pontifex.Tests.RR.RRReliableDirect.Harness;
using Scriba;

namespace Pontifex.Tests.RR.RRReliableDirect;

/// <summary>
/// Pins the factory binding of the in-process RR reliable transport: defaults, explicit TTLs, and
/// role validation.
/// </summary>
[TestFixture]
[Category("Direct.RRReliable")]
public sealed class RRReliableDirectBindingTests
{
    private static readonly ILogger Logger = new Logger([]);

    [Test]
    public void CreateClient_WithoutParameters_UsesTheThreeSecondDefault()
    {
        var transport = Create(TransportRole.Client, "node-1");
        var client = (IRRReliableClientTransport)transport;

        Assert.Multiple(() =>
        {
            Assert.That(client.RequestTtl, Is.EqualTo(TimeSpan.FromSeconds(3)));
            Assert.That(client.MessageMaxByteSize, Is.EqualTo(RRDirectInfo.MessageMaxByteSize));
            Assert.That(client.ServerEndPoint, Is.Not.Null);
        });
    }

    [Test]
    public void CreateServer_WithoutParameters_UsesTheThreeSecondDefault()
    {
        var transport = Create(TransportRole.Server, "node-1");
        var server = (IRRReliableServerTransport)transport;

        Assert.Multiple(() =>
        {
            Assert.That(server.ResponseTtl, Is.EqualTo(TimeSpan.FromSeconds(3)));
            Assert.That(server.MessageMaxByteSize, Is.EqualTo(RRDirectInfo.MessageMaxByteSize));
        });
    }

    [Test]
    public void Create_WithExplicitTtls_UsesThem()
    {
        var client = (IRRReliableClientTransport)Create(
            TransportRole.Client, "node-1", context => context.WithInt64("request_ttl", 9));
        var server = (IRRReliableServerTransport)Create(
            TransportRole.Server, "node-1", context => context.WithInt64("response_ttl", 7));

        Assert.Multiple(() =>
        {
            Assert.That(client.RequestTtl, Is.EqualTo(TimeSpan.FromSeconds(9)));
            Assert.That(server.ResponseTtl, Is.EqualTo(TimeSpan.FromSeconds(7)));
        });
    }

    [Test]
    public void CreateServer_WithRequestTtl_Throws()
    {
        Assert.That(
            () => Create(TransportRole.Server, "node-1",
                context => context.WithInt64("request_ttl", 5)),
            Throws.TypeOf<ArgumentException>());
    }

    [Test]
    public void CreateClient_WithResponseTtl_Throws()
    {
        Assert.That(
            () => Create(TransportRole.Client, "node-1",
                context => context.WithInt64("response_ttl", 5)),
            Throws.TypeOf<InvalidOperationException>());
    }

    [Test]
    public void CreateRegistration_DeclaresTheReliableScheme()
    {
        var registration = RRReliableDirectBinding.CreateRegistration();

        Assert.Multiple(() =>
        {
            Assert.That(registration.Name, Is.EqualTo("direct-rr-reliable"));
            Assert.That(registration.Type, Is.EqualTo(TransportType.RRReliable));
        });
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        Logger.Dispose();
    }

    private static ITransport Create(
        TransportRole role,
        string id,
        Action<TestBuildContext>? configure = null)
    {
        var context = new TestBuildContext(role, Logger, MemoryRental.Shared)
            .WithString("id", id);
        configure?.Invoke(context);
        return RRReliableDirectBinding.CreateRegistration().Create(context);
    }
}
