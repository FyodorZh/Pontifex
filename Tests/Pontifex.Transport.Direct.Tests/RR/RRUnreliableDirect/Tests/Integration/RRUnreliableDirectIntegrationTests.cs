using Actuarius.Memory;
using Pontifex.Factory.Registration;
using Pontifex.RR.Unreliable;
using Pontifex.RR.Unreliable.Direct;
using Pontifex.Tests.RR.RRUnreliableDirect.Harness;
using Pontifex.Tests.TestSupport;
using Scriba;

namespace Pontifex.Tests.RR.RRUnreliableDirect;

/// <summary>
/// Binding-created round trips of the in-process RR unreliable transport, including a real netem
/// reconnect.
/// </summary>
[TestFixture]
[Category("Direct.RRUnreliable")]
[Category("Direct.RRUnreliable.Integration")]
public sealed class RRUnreliableDirectIntegrationTests
{
    private static readonly ILogger Logger = new Logger([]);
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly List<ITransport> _transports = new();

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        Logger.Dispose();
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = _transports.Count - 1; i >= 0; i--)
        {
            _transports[i].Stop();
        }

        _transports.Clear();
    }

    [Test]
    public void BindingCreatedPair_RoundTripsARequest()
    {
        string name = NewName();
        var server = (IRRUnreliableServerTransport)Create(TransportRole.Server, name, null);
        var client = (IRRUnreliableClientTransport)Create(TransportRole.Client, name, null);
        using var serverHandler = new RecordingServerHandler(MemoryRental.Shared);
        using var clientHandler = new RecordingClientHandler();

        Assert.That(server.Init(serverHandler), Is.True);
        Assert.That(server.Start(_ => { }), Is.True);
        Assert.That(client.Start(_ => { }), Is.True);
        _transports.Add(client);
        _transports.Add(server);

        Assert.That(client.Request(TestMessages.Int(21), clientHandler), Is.EqualTo(SendResult.Ok));
        Assert.That(serverHandler.Requested.Wait(Timeout), Is.True);
        Assert.That(clientHandler.Responded.Wait(Timeout), Is.True);
        Assert.That(clientHandler.LastResponseValue, Is.EqualTo(22));
    }

    [Test]
    public void BindingCreatedClient_WithNetemReconnectsAfterAChannelLoss()
    {
        string name = NewName();
        var server = (IRRUnreliableServerTransport)Create(TransportRole.Server, name, null);
        var clientTransport = Create(
            TransportRole.Client,
            name,
            TestStructValue.Delivery("netem"));
        var client = (IRRUnreliableClientTransport)clientTransport;
        using var serverHandler = new RecordingServerHandler(MemoryRental.Shared);
        using var firstHandler = new RecordingClientHandler();
        using var secondHandler = new RecordingClientHandler();

        Assert.That(server.Init(serverHandler), Is.True);
        Assert.That(server.Start(_ => { }), Is.True);
        Assert.That(client.Start(_ => { }), Is.True);
        _transports.Add(clientTransport);
        _transports.Add(server);

        Assert.That(client.Request(TestMessages.Int(1), firstHandler), Is.EqualTo(SendResult.Ok));
        Assert.That(firstHandler.Responded.Wait(Timeout), Is.True);

        var directClient = (RRUnreliableDirectClient)clientTransport;
        Assert.That(directClient.CurrentChannel, Is.Not.Null);
        directClient.CurrentChannel!.Dispose();
        Assert.That(directClient.CurrentChannel, Is.Null);

        Assert.That(client.Request(TestMessages.Int(2), secondHandler), Is.EqualTo(SendResult.Ok));
        Assert.That(secondHandler.Responded.Wait(Timeout), Is.True);
        Assert.That(serverHandler.RequestCount, Is.EqualTo(2));
    }

    private static string NewName()
    {
        return "rr-integration-" + Guid.NewGuid().ToString("N");
    }

    private static ITransport Create(
        TransportRole role,
        string id,
        TestStructValue? delivery)
    {
        var context = new TestBuildContext(role, Logger, MemoryRental.Shared)
            .WithString("id", id);

        if (delivery != null)
        {
            context.WithStruct("delivery", delivery);
        }

        return RRUnreliableDirectBinding.CreateRegistration().Create(context);
    }
}
