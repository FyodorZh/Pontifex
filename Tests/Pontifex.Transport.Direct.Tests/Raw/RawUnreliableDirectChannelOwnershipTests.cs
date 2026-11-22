using System;
using System.Collections.Generic;
using System.Threading;
using Actuarius.Memory;
using Pontifex;
using Pontifex.Raw.Unreliable;
using Pontifex.Raw.Unreliable.Direct;
using Pontifex.StopReasons;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Tests.Raw;

/// <summary>
/// Pins the channel ownership of the in-process unreliable carrier: a channel that stops must leave no entry
/// behind in the server registry.
/// </summary>
[TestFixture]
public sealed class RawUnreliableDirectChannelOwnershipTests
{
    private static readonly ILogger Logger = new Logger([]);

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
            try
            {
                _transports[i].Stop();
            }
            catch (Exception)
            {
            }
        }

        _transports.Clear();
    }

    [Test]
    public void ClientStop_RemovesTheDisposedChannelFromTheServerRegistry()
    {
        var server = StartServer(out var serverName);
        var client = StartClient(serverName, out var handler);

        Assert.That(handler.Endpoint, Is.Not.Null);
        var message = MemoryRental.Shared.CollectablePool.Acquire<UnionDataList>();
        message.PutLast(7);
        Assert.That(handler.Endpoint!.Send(message), Is.EqualTo(SendResult.Ok));

        WaitFor(() => server.RegisteredChannelCount == 1,
            "The client channel was not registered with the server.");

        Assert.That(client.Stop(), Is.True);

        WaitFor(() => server.RegisteredChannelCount == 0,
            "The disposed channel remained in the server registry.");
    }

    [Test]
    public void ServerStop_RemovesEveryRegisteredChannel()
    {
        var server = StartServer(out var serverName);
        StartClient(serverName, out var handler);

        Assert.That(handler.Endpoint, Is.Not.Null);
        var message = MemoryRental.Shared.CollectablePool.Acquire<UnionDataList>();
        message.PutLast(11);
        Assert.That(handler.Endpoint!.Send(message), Is.EqualTo(SendResult.Ok));

        WaitFor(() => server.RegisteredChannelCount == 1,
            "The client channel was not registered with the server.");

        Assert.That(server.Stop(), Is.True);

        WaitFor(() => server.RegisteredChannelCount == 0,
            "A registered channel survived the server stop.");
    }

    private RawUnreliableDirectServerTransport StartServer(out string serverName)
    {
        serverName = "ownership-" + Guid.NewGuid().ToString("N");
        var server = new RawUnreliableDirectServerTransport(serverName, Logger, MemoryRental.Shared);
        Assert.That(server.Init(_ => null), Is.True);
        Assert.That(server.Start(_ => { }), Is.True);
        _transports.Add(server);
        return server;
    }

    private RawUnreliableDirectClientTransport StartClient(string serverName, out RecordingHandler handler)
    {
        handler = new RecordingHandler();
        var recording = handler;
        var client = new RawUnreliableDirectClientTransport(serverName, Logger, MemoryRental.Shared);
        Assert.That(client.Init(recording), Is.True);
        Assert.That(client.Start(_ => { }), Is.True);
        _transports.Add(client);

        WaitFor(() => recording.Endpoint != null, "OnStarted was not invoked.");
        return client;
    }

    private static void WaitFor(Func<bool> condition, string message)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                Assert.Fail(message);
            }

            Thread.Sleep(1);
        }
    }

    private sealed class RecordingHandler : IRawUnreliableHandler
    {
        public IRawUnreliableEndpoint? Endpoint { get; private set; }

        public void OnStarted(IRawUnreliableEndpoint endpoint)
        {
            Endpoint = endpoint;
        }

        public void OnReceived(UnionDataList receivedBuffer)
        {
            receivedBuffer.Release();
        }

        public void OnStopped(StopReason reason)
        {
        }
    }
}
