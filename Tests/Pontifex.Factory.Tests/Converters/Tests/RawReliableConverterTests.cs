using System;
using System.Collections.Generic;
using System.Text;
using Actuarius.Memory;
using NUnit.Framework;
using Pontifex.Converters;
using Pontifex.Factory.Tests.Converters.Harness;
using Pontifex.Raw.Reliable;
using Pontifex.StopReasons;
using Pontifex.Utils;
using Status = Pontifex.Raw.Reliable.RawReliableHandshake.HandshakeStatus;

namespace Pontifex.Factory.Tests.Converters;

[TestFixture]
[Category("Factory.Converters")]
public sealed class RawReliableConverterTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private sealed class Pair : IDisposable
    {
        public Pair(
            FakeRawUnreliableNetwork network,
            RawUnreliableToRawReliableClient client,
            RawUnreliableToRawReliableServer server,
            RecordingRawReliableClientHandler clientHandler,
            RecordingRawReliableServerHandler serverHandler,
            RecordingRawReliableServerHandlerFactory factory)
        {
            Network = network;
            Client = client;
            Server = server;
            ClientHandler = clientHandler;
            ServerHandler = serverHandler;
            Factory = factory;
        }

        public FakeRawUnreliableNetwork Network { get; }

        public RawUnreliableToRawReliableClient Client { get; }

        public RawUnreliableToRawReliableServer Server { get; }

        public RecordingRawReliableClientHandler ClientHandler { get; }

        public RecordingRawReliableServerHandler ServerHandler { get; }

        public RecordingRawReliableServerHandlerFactory Factory { get; }

        public void Dispose()
        {
            Client.Stop(new UserIntention("test"));
            Server.Stop(new UserIntention("test"));
            ClientHandler.DisposeReceived();
            ServerHandler.DisposeReceived();
        }
    }

    [Test]
    public void Handshake_Accepted_DeliversApplicationMessagesInBothDirections()
    {
        using var pair = CreatePair();

        WaitConnected(pair.ClientHandler, "client");
        WaitConnected(pair.ServerHandler, "server");
        Assert.That(pair.Factory.Calls, Is.EqualTo(1));

        Assert.That(pair.ClientHandler.Endpoint, Is.Not.Null);
        Assert.That(
            pair.ClientHandler.Endpoint!.Send(Text("client-to-server")),
            Is.EqualTo(SendResult.Ok));
        WaitReceived(pair.ServerHandler, "server");
        Assert.That(TextOf(pair.ServerHandler.Received[0]), Is.EqualTo("client-to-server"));

        Assert.That(pair.ServerHandler.Endpoint, Is.Not.Null);
        Assert.That(
            pair.ServerHandler.Endpoint!.Send(Text("server-to-client")),
            Is.EqualTo(SendResult.Ok));
        WaitReceived(pair.ClientHandler, "client");
        Assert.That(TextOf(pair.ClientHandler.Received[0]), Is.EqualTo("server-to-client"));
    }

    [Test]
    public void HandshakeMessage_IsNotDeliveredToApplicationHandlers()
    {
        using var pair = CreatePair();

        WaitConnected(pair.ClientHandler, "client");
        WaitConnected(pair.ServerHandler, "server");

        pair.ClientHandler.Endpoint!.Send(Text("payload"));
        WaitReceived(pair.ServerHandler, "server");

        Assert.That(pair.ServerHandler.Received, Has.Count.EqualTo(1));
        Assert.That(TextOf(pair.ServerHandler.Received[0]), Is.EqualTo("payload"));
        Assert.That(pair.ClientHandler.Received, Is.Empty);
    }

    [Test]
    public void Client_HandshakeRequest_DecodesVersionNameAndDeliveryMaximum()
    {
        var network = new FakeRawUnreliableNetwork();
        var peer = new RawUnreliableDeliveryPeer(4096);
        network.Server.Init(_ => peer);
        Assert.That(network.Server.Start(_ => { }), Is.True);

        short requestVersion = 0;
        string? requestName = null;
        int requestMax = 0;
        bool parsed = false;
        peer.UserReceived += data =>
        {
            try
            {
                parsed = ConverterHandshake.TryParseRequest(
                    data,
                    out requestVersion,
                    out requestName,
                    out requestMax);
            }
            finally
            {
                data.Release();
            }
        };

        var clientHandler = new RecordingRawReliableClientHandler();
        var client = new RawUnreliableToRawReliableClient(network.Client, null, null);
        Assert.That(client.Init(clientHandler), Is.True);
        Assert.That(client.Start(_ => { }), Is.True);

        try
        {
            Assert.That(peer.ReceivedEvent.Wait(Timeout), Is.True, "the server did not receive the handshake request");

            Assert.That(parsed, Is.True, "the first peer payload must be a well-formed handshake request");
            Assert.That(requestVersion, Is.EqualTo(ConverterHandshake.Version));
            Assert.That(requestName, Is.EqualTo(ConverterHandshake.Name));
            // The request carries the client delivery-manager maximum, a derived
            // chunking bound; recompute it the same way the converter does.
            Assert.That(
                requestMax,
                Is.EqualTo(DeliveryMax(network.Client.MessageMaxByteSize)));
        }
        finally
        {
            client.Stop(new UserIntention("test"));
            network.Server.Stop(new UserIntention("test"));
            clientHandler.DisposeReceived();
        }
    }

    [Test]
    public void Client_DoesNotRaiseOnConnectedBeforeTheHandshakeResponse()
    {
        var network = new FakeRawUnreliableNetwork();
        var peer = new RawUnreliableDeliveryPeer(4096);
        network.Server.Init(_ => peer);
        Assert.That(network.Server.Start(_ => { }), Is.True);

        var clientHandler = new RecordingRawReliableClientHandler();
        var client = new RawUnreliableToRawReliableClient(network.Client, null, null);
        Assert.That(client.Init(clientHandler), Is.True);
        Assert.That(client.Start(_ => { }), Is.True);

        try
        {
            Assert.That(peer.ReceivedEvent.Wait(Timeout), Is.True, "the server did not receive the handshake request");
            Assert.That(clientHandler.ConnectedCount, Is.Zero);
            Assert.That(clientHandler.StoppedCount, Is.Zero);

            Assert.That(peer.Send(ConverterHandshake.BuildAccepted(4096)), Is.EqualTo(SendResult.Ok));

            Assert.That(clientHandler.ConnectedEvent.Wait(Timeout), Is.True, "the client did not connect after the response");
            Assert.That(clientHandler.ConnectedCount, Is.EqualTo(1));
        }
        finally
        {
            client.Stop(new UserIntention("test"));
            network.Server.Stop(new UserIntention("test"));
            clientHandler.DisposeReceived();
        }
    }

    [Test]
    public void Client_WithNameMismatchReject_StopsWithProtocolMismatchAndNoConnected()
    {
        var network = new FakeRawUnreliableNetwork();
        var peer = new RawUnreliableDeliveryPeer(4096);
        network.Server.Init(_ => peer);
        Assert.That(network.Server.Start(_ => { }), Is.True);

        var clientHandler = new RecordingRawReliableClientHandler();
        var client = new RawUnreliableToRawReliableClient(network.Client, null, null);
        Assert.That(client.Init(clientHandler), Is.True);
        Assert.That(client.Start(_ => { }), Is.True);

        try
        {
            Assert.That(peer.ReceivedEvent.Wait(Timeout), Is.True, "the server did not receive the handshake request");
            Assert.That(peer.Send(ConverterHandshake.BuildReject(
                    ConverterHandshake.Version,
                    "not-converter",
                    Status.Rejected,
                    "name mismatch")),
                Is.EqualTo(SendResult.Ok));

            Assert.That(clientHandler.StoppedEvent.Wait(Timeout), Is.True, "the client did not stop after the reject");
            Assert.That(clientHandler.ConnectedCount, Is.Zero);
            Assert.That(clientHandler.DisconnectedCount, Is.Zero);
            Assert.That(clientHandler.StoppedCount, Is.EqualTo(1));
            Assert.That(clientHandler.StopReason, Is.TypeOf<ProtocolMismatch>());
        }
        finally
        {
            client.Stop(new UserIntention("test"));
            network.Server.Stop(new UserIntention("test"));
            clientHandler.DisposeReceived();
        }
    }

    [TestCase((int)Status.VersionMismatch, typeof(ProtocolMismatch))]
    [TestCase((int)Status.NameMismatch, typeof(ProtocolMismatch))]
    [TestCase((int)Status.Rejected, typeof(HandshakeRejected))]
    [TestCase((int)Status.CapacityExceeded, typeof(HandshakeRejected))]
    public void Client_WithRejectStatus_MapsToTheExpectedStopReason(
        int statusValue,
        Type expectedReason)
    {
        var status = (Status)statusValue;
        var network = new FakeRawUnreliableNetwork();
        var peer = new RawUnreliableDeliveryPeer(4096);
        network.Server.Init(_ => peer);
        Assert.That(network.Server.Start(_ => { }), Is.True);

        var clientHandler = new RecordingRawReliableClientHandler();
        var client = new RawUnreliableToRawReliableClient(network.Client, null, null);
        Assert.That(client.Init(clientHandler), Is.True);
        Assert.That(client.Start(_ => { }), Is.True);

        try
        {
            Assert.That(peer.ReceivedEvent.Wait(Timeout), Is.True, "the server did not receive the handshake request");
            Assert.That(peer.Send(ConverterHandshake.BuildReject(
                    ConverterHandshake.Version,
                    "converter",
                    status,
                    "rejected")),
                Is.EqualTo(SendResult.Ok));

            Assert.That(clientHandler.StoppedEvent.Wait(Timeout), Is.True, "the client did not stop after the reject");
            Assert.That(clientHandler.ConnectedCount, Is.Zero);
            Assert.That(clientHandler.StopReason, Is.TypeOf(expectedReason));
        }
        finally
        {
            client.Stop(new UserIntention("test"));
            network.Server.Stop(new UserIntention("test"));
            clientHandler.DisposeReceived();
        }
    }

    [Test]
    public void Server_WithVersionMismatch_RejectsWithoutCreatingASession()
    {
        AssertServerRejectsHandshake(
            version: 999,
            name: "converter",
            expectedText: "version mismatch");
    }

    [Test]
    public void Server_WithNameMismatch_RejectsWithoutCreatingASession()
    {
        AssertServerRejectsHandshake(
            version: ConverterHandshake.Version,
            name: "not-converter",
            expectedText: "name mismatch");
    }

    [Test]
    public void Handshake_NegotiatesTheMinimumDeliveryManagerMaximum()
    {
        const int clientInnerMax = 100;
        const int serverInnerMax = 140;
        using var pair = CreatePair(clientInnerMax, serverInnerMax);

        WaitConnected(pair.ClientHandler, "client");
        WaitConnected(pair.ServerHandler, "server");

        var expected = Math.Min(DeliveryMax(clientInnerMax), DeliveryMax(serverInnerMax));
        Assert.That(pair.ClientHandler.Endpoint!.MessageMaxByteSize, Is.EqualTo(expected));
        Assert.That(pair.ServerHandler.Endpoint!.MessageMaxByteSize, Is.EqualTo(expected));

        Assert.That(
            pair.ClientHandler.Endpoint.Send(BufferOfSize(expected + 1)),
            Is.EqualTo(SendResult.MessageTooBig));
        Assert.That(
            pair.ServerHandler.Endpoint.Send(BufferOfSize(expected + 1)),
            Is.EqualTo(SendResult.MessageTooBig));
    }

    [Test]
    public void Handshake_WhenServerMaximumIsSmaller_ClientLowersToIt()
    {
        const int clientInnerMax = 140;
        const int serverInnerMax = 100;
        using var pair = CreatePair(clientInnerMax, serverInnerMax);

        WaitConnected(pair.ClientHandler, "client");
        WaitConnected(pair.ServerHandler, "server");

        var expected = Math.Min(DeliveryMax(clientInnerMax), DeliveryMax(serverInnerMax));
        Assert.That(expected, Is.EqualTo(DeliveryMax(serverInnerMax)),
            "precondition: the server side is the smaller maximum");
        Assert.That(pair.ClientHandler.Endpoint!.MessageMaxByteSize, Is.EqualTo(expected));
        Assert.That(pair.ServerHandler.Endpoint!.MessageMaxByteSize, Is.EqualTo(expected));
    }

    [Test]
    public void AdmittedSession_DeliversTheOrderedLifecycleExactlyOnce()
    {
        using var pair = CreatePair();
        WaitConnected(pair.ClientHandler, "client");
        WaitConnected(pair.ServerHandler, "server");

        pair.Client.Stop(new UserIntention("client-stop"));
        Assert.That(pair.ClientHandler.StoppedEvent.Wait(Timeout), Is.True);
        pair.Server.Stop(new UserIntention("server-stop"));
        Assert.That(pair.ServerHandler.StoppedEvent.Wait(Timeout), Is.True);

        Assert.That(pair.ClientHandler.Lifecycle, Is.EqualTo(new[] { "Connected", "Disconnected", "Stopped" }));
        Assert.That(pair.ServerHandler.Lifecycle, Is.EqualTo(new[] { "Connected", "Disconnected", "Stopped" }));
        Assert.That(pair.ClientHandler.ConnectedCount, Is.EqualTo(1));
        Assert.That(pair.ClientHandler.DisconnectedCount, Is.EqualTo(1));
        Assert.That(pair.ClientHandler.StoppedCount, Is.EqualTo(1));
        Assert.That(pair.ServerHandler.ConnectedCount, Is.EqualTo(1));
        Assert.That(pair.ServerHandler.DisconnectedCount, Is.EqualTo(1));
        Assert.That(pair.ServerHandler.StoppedCount, Is.EqualTo(1));

        pair.Client.Stop(new UserIntention("again"));
        pair.Server.Stop(new UserIntention("again"));
        Assert.That(pair.ClientHandler.StoppedCount, Is.EqualTo(1));
        Assert.That(pair.ServerHandler.StoppedCount, Is.EqualTo(1));
    }

    private static void AssertServerRejectsHandshake(short version, string name, string expectedText)
    {
        var network = new FakeRawUnreliableNetwork();
        var peer = new RawUnreliableDeliveryPeer(4096);
        network.Client.Init(peer);

        var serverHandler = new RecordingRawReliableServerHandler();
        var factory = new RecordingRawReliableServerHandlerFactory(_ => serverHandler);
        var server = new RawUnreliableToRawReliableServer(network.Server, null, null);
        Assert.That(server.Init(factory), Is.True);
        Assert.That(server.Start(_ => { }), Is.True);
        Assert.That(network.Client.Start(_ => { }), Is.True);

        Status? actualStatus = null;
        string? actualText = null;
        peer.UserReceived += data =>
        {
            try
            {
                Assert.That(ConverterHandshake.TryReadStatus(data, out var status), Is.True);
                Assert.That(ConverterHandshake.TryParseReject(data, out var serverVersion, out var serverName,
                    out var parsedStatus, out var text), Is.True);
                Assert.That(serverVersion, Is.EqualTo(ConverterHandshake.Version));
                Assert.That(serverName, Is.EqualTo(ConverterHandshake.Name));
                actualStatus = parsedStatus;
                actualText = text;
            }
            finally
            {
                data.Release();
            }
        };

        try
        {
            var request = BuildRequest(version, name, 512);
            Assert.That(peer.Send(request), Is.EqualTo(SendResult.Ok));

            Assert.That(peer.ReceivedEvent.Wait(Timeout), Is.True, "the server did not answer the invalid handshake");
            Assert.That(actualStatus, Is.EqualTo(Status.Rejected));
            Assert.That(actualText, Is.EqualTo(expectedText));
            Assert.That(factory.Calls, Is.Zero);
            Assert.That(serverHandler.ConnectedCount, Is.Zero);
        }
        finally
        {
            network.Client.Stop(new UserIntention("test"));
            server.Stop(new UserIntention("test"));
            serverHandler.DisposeReceived();
        }
    }

    private static Pair CreatePair(int clientInnerMax = 4096, int serverInnerMax = 4096)
    {
        var network = new FakeRawUnreliableNetwork(clientInnerMax, serverInnerMax);
        var clientHandler = new RecordingRawReliableClientHandler();
        var serverHandler = new RecordingRawReliableServerHandler();
        var factory = new RecordingRawReliableServerHandlerFactory(_ => serverHandler);

        var server = new RawUnreliableToRawReliableServer(network.Server, null, null);
        var client = new RawUnreliableToRawReliableClient(network.Client, null, null);

        Assert.That(server.Init(factory), Is.True);
        Assert.That(client.Init(clientHandler), Is.True);
        Assert.That(server.Start(_ => { }), Is.True);
        Assert.That(client.Start(_ => { }), Is.True);

        return new Pair(network, client, server, clientHandler, serverHandler, factory);
    }

    private static void WaitConnected(RecordingRawReliableClientHandler handler, string side)
    {
        Assert.That(handler.ConnectedEvent.Wait(Timeout), Is.True, side + " did not connect");
    }

    private static void WaitConnected(RecordingRawReliableServerHandler handler, string side)
    {
        Assert.That(handler.ConnectedEvent.Wait(Timeout), Is.True, side + " did not connect");
    }

    private static void WaitReceived(RecordingRawReliableClientHandler handler, string side)
    {
        Assert.That(handler.ReceivedEvent.Wait(Timeout), Is.True, side + " did not receive a message");
    }

    private static void WaitReceived(RecordingRawReliableServerHandler handler, string side)
    {
        Assert.That(handler.ReceivedEvent.Wait(Timeout), Is.True, side + " did not receive a message");
    }

    private static UnionDataList Text(string text)
    {
        var data = MemoryRental.Shared.CollectablePool.Acquire<UnionDataList>();
        data.PutLast(new UnionData(new MultiRefByteArray(Encoding.UTF8.GetBytes(text))));
        return data;
    }

    private static string TextOf(UnionDataList data)
    {
        Assert.That(data.Elements, Has.Count.EqualTo(1));
        var bytes = data.Elements[0].Bytes;
        Assert.That(bytes, Is.Not.Null);
        return Encoding.UTF8.GetString(bytes!.ReadOnlyArray, bytes.Offset, bytes.Count);
    }

    private static UnionDataList BuildRequest(short version, string name, int max)
    {
        var data = MemoryRental.Shared.CollectablePool.Acquire<UnionDataList>();
        data.PutLast(new UnionData(version));
        data.PutLast(new UnionData(new MultiRefByteArray(Encoding.UTF8.GetBytes(name))));
        data.PutLast(new UnionData(max));
        return data;
    }

    private static int DeliveryMax(int innerMessageMaxByteSize)
    {
        var dm = new Pontifex.Delivery.DeliveryManager(
            innerMessageMaxByteSize,
            MemoryRental.Shared.ByteArraysPool,
            MemoryRental.Shared.CollectablePool);
        return dm.DeliveryMaxByteSize;
    }

    private static UnionDataList BufferOfSize(int totalSize)
    {
        var probe = MemoryRental.Shared.CollectablePool.Acquire<UnionDataList>();
        probe.PutLast(new UnionData(new MultiRefByteArray(new byte[1])));
        var overhead = probe.GetDataSize() - 1;
        probe.Release();

        var payload = totalSize - overhead;
        var result = MemoryRental.Shared.CollectablePool.Acquire<UnionDataList>();
        result.PutLast(new UnionData(new MultiRefByteArray(new byte[payload])));
        return result;
    }
}
