using Actuarius.Memory;
using Pontifex.Factory.Registration;
using Pontifex.RR.Unreliable;
using Pontifex.RR.Unreliable.Direct;
using Pontifex.Tests.TestSupport;
using Scriba;

namespace Pontifex.Tests.RR.RRUnreliableDirect;

/// <summary>
/// Pins the factory binding of the in-process RR unreliable transport: defaults, explicit TTLs,
/// role validation, and eager delivery validation.
/// </summary>
[TestFixture]
[Category("Direct.RRUnreliable")]
public sealed class RRUnreliableDirectBindingTests
{
    private static readonly ILogger Logger = new Logger([]);

    [Test]
    public void CreateClient_WithoutParameters_UsesTheThreeSecondDefault()
    {
        var transport = Create(TransportRole.Client, "node-1");
        var client = (IRRUnreliableClientTransport)transport;

        Assert.Multiple(() =>
        {
            Assert.That(client.RequestTtl, Is.EqualTo(TimeSpan.FromSeconds(3)));
            Assert.That(client.MessageMaxByteSize, Is.EqualTo(1024 * 1024));
            Assert.That(client.ServerEndPoint, Is.Not.Null);
        });
    }

    [Test]
    public void CreateServer_WithoutParameters_UsesTheThreeSecondDefault()
    {
        var transport = Create(TransportRole.Server, "node-1");
        var server = (IRRUnreliableServerTransport)transport;

        Assert.Multiple(() =>
        {
            Assert.That(server.ResponseTtl, Is.EqualTo(TimeSpan.FromSeconds(3)));
            Assert.That(server.MessageMaxByteSize, Is.EqualTo(1024 * 1024));
        });
    }

    [Test]
    public void Create_WithExplicitTtls_UsesThem()
    {
        var client = (IRRUnreliableClientTransport)Create(
            TransportRole.Client, "node-1", context => context.WithInt64("request_ttl", 9));
        var server = (IRRUnreliableServerTransport)Create(
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
    public void CreateServer_WithDelivery_Throws()
    {
        Assert.That(
            () => Create(TransportRole.Server, "node-1",
                context => context.WithStruct("delivery", TestStructValue.Delivery("perfect"))),
            Throws.TypeOf<ArgumentException>());
    }

    [Test]
    public void CreateClient_WithUnknownDeliveryType_Throws()
    {
        Assert.That(
            () => Create(TransportRole.Client, "node-1",
                context => context.WithStruct("delivery", TestStructValue.Delivery("bogus"))),
            Throws.TypeOf<ArgumentException>());
    }

    [Test]
    public void CreateClient_WithFourStateWithoutParameters_ThrowsEagerly()
    {
        var delivery = TestStructValue.Delivery("netem").WithString("lossmodel", "fourState");

        Assert.That(
            () => Create(TransportRole.Client, "node-1",
                context => context.WithStruct("delivery", delivery)),
            Throws.TypeOf<ArgumentException>());
    }

    [Test]
    public void CreateClient_WithGilbertElliotWithoutParameters_ThrowsEagerly()
    {
        var delivery = TestStructValue.Delivery("netem").WithString("lossmodel", "gilbertElliot");

        Assert.That(
            () => Create(TransportRole.Client, "node-1",
                context => context.WithStruct("delivery", delivery)),
            Throws.TypeOf<ArgumentException>());
    }

    [Test]
    public void CreateClient_WithPerfectDelivery_ProducesAStartedTransport()
    {
        var transport = Create(TransportRole.Client, "node-1",
            context => context.WithStruct("delivery", TestStructValue.Delivery("perfect")));
        var client = (IRRUnreliableClientTransport)transport;

        Assert.That(client.Start(_ => { }), Is.True);
        Assert.That(client.Stop(), Is.True);
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
        return RRUnreliableDirectBinding.CreateRegistration().Create(context);
    }
}
