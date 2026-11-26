using System;
using System.Collections.Generic;
using Actuarius.Memory;
using Pontifex.Factory.Registration;
using Pontifex.RR.Reliable;
using Pontifex.RR.Reliable.Direct;
using Pontifex.Tests.RR.RRReliableDirect.Harness;
using Pontifex.Tests.TestSupport;
using Scriba;

namespace Pontifex.Tests.RR.RRReliableDirect;

/// <summary>
/// Binding-created round trip of the in-process RR reliable transport.
/// </summary>
[TestFixture]
[Category("Direct.RRReliable")]
[Category("Direct.RRReliable.Integration")]
public sealed class RRReliableDirectIntegrationTests
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
    public void BindingCreatedPair_RoundTripsARequestAndCompletion()
    {
        string name = "rr-reliable-integration-" + Guid.NewGuid().ToString("N");
        var server = (IRRReliableServerTransport)Create(TransportRole.Server, name);
        var client = (IRRReliableClientTransport)Create(TransportRole.Client, name);
        using var serverHandler = new RecordingReliableServerHandler(
            MemoryRental.Shared, ReliableServerMode.RespondAndFinish);
        using var clientHandler = new RecordingReliableClientHandler();

        Assert.That(server.Init(serverHandler), Is.True);
        Assert.That(server.Start(_ => { }), Is.True);
        Assert.That(client.Start(_ => { }), Is.True);
        _transports.Add(client);
        _transports.Add(server);

        Assert.That(client.Request(TestMessages.Int(21), clientHandler), Is.EqualTo(SendResult.Ok));
        Assert.That(serverHandler.Requested.Wait(Timeout), Is.True,
            "the server must receive the request through the compiled binding");
        Assert.That(clientHandler.Responded.Wait(Timeout), Is.True,
            "the client must receive the response through the compiled binding");
        Assert.That(clientHandler.Finished.Wait(Timeout), Is.True,
            "the client must receive the completion through the compiled binding");

        Assert.Multiple(() =>
        {
            Assert.That(clientHandler.ResponseValues, Is.EqualTo(new[] { 22 }));
            Assert.That(clientHandler.Finishes, Is.EqualTo(new[] { RRReliableCompletionType.Normal }));
        });
    }

    private static ITransport Create(TransportRole role, string id)
    {
        var context = new TestBuildContext(role, Logger, MemoryRental.Shared)
            .WithString("id", id);
        return RRReliableDirectBinding.CreateRegistration().Create(context);
    }
}
