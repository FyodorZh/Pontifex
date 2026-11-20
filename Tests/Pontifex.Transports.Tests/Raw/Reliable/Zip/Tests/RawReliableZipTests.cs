using Pontifex.Raw.Reliable.Protocols;
using Pontifex.Raw.Reliable.Zip;
using Pontifex.StopReasons;
using Pontifex.Tests.Raw.Reliable.Zip.Harness;
using Pontifex.Utils;

namespace Pontifex.Tests.Raw.Reliable.Zip;

/// <summary>
/// Contract tests for the zip reliable wrapper: its own <c>"zip"</c> negotiation dataset, admission over any
/// reliable transport, and transparent compression of application payloads.
/// </summary>
[TestFixture]
[Category("Zip")]
public sealed class RawReliableZipTests
{
    [Test]
    public void AcceptedNegotiation_RoundTripsDataInBothDirections()
    {
        using var pair = new ZipPair();
        pair.Start();

        var serverHandler = pair.ServerHandler;

        Assert.Multiple(() =>
        {
            Assert.That(pair.Factory.Calls, Is.EqualTo(1));
            Assert.That(pair.ClientHandler.ConnectedCount, Is.EqualTo(1));
            Assert.That(serverHandler, Is.Not.Null);
            Assert.That(serverHandler!.ConnectedCount, Is.EqualTo(1));
            Assert.That(pair.ClientHandler.Endpoint!.IsConnected, Is.True);
            Assert.That(serverHandler.Endpoint!.IsConnected, Is.True);
            Assert.That(pair.ClientHandler.Endpoint.MessageMaxByteSize, Is.EqualTo(4096));
        });

        Assert.That(ZipPayload.Describe(pair.InnerClient.Endpoint!.Sent[0]), Is.EqualTo(
            new[] { "h:1", "s:zip", "b:0" }));
        Assert.That(ZipPayload.Describe(pair.ServerSession!.Endpoint.Sent[0]), Is.EqualTo(
            new[] { "h:1", "s:zip", "b:1" }));

        Assert.That(pair.ClientHandler.Endpoint.Send(ZipPayload.Create("hello")),
            Is.EqualTo(SendResult.Ok));
        Assert.That(serverHandler.Received, Has.Count.EqualTo(1));
        Assert.That(serverHandler.Received[0], Is.EqualTo(new[] { "s:hello" }));

        Assert.That(serverHandler.Endpoint!.Send(ZipPayload.Create("world")),
            Is.EqualTo(SendResult.Ok));
        Assert.That(pair.ClientHandler.Received, Has.Count.EqualTo(1));
        Assert.That(pair.ClientHandler.Received[0], Is.EqualTo(new[] { "s:world" }));
    }

    [Test]
    public void CompressiblePayload_IsSentSmallerThanTheUncompressedDataset()
    {
        using var pair = new ZipPair();
        pair.Start();

        string text = new string('A', 2000);
        UnionDataList payload = ZipPayload.Create(text);
        int uncompressedSize = payload.GetDataSize();

        Assert.That(pair.ClientHandler.Endpoint!.Send(payload), Is.EqualTo(SendResult.Ok));

        Assert.That(pair.InnerClient.Endpoint!.Sent, Has.Count.EqualTo(2));
        UnionDataList wire = pair.InnerClient.Endpoint.Sent[1];

        Assert.Multiple(() =>
        {
            Assert.That(uncompressedSize, Is.GreaterThan(text.Length),
                "the uncompressed dataset must be larger than the raw text");
            Assert.That(wire.Elements, Has.Count.EqualTo(1),
                "the compressed payload is a single byte-array element");
            Assert.That(wire.Elements[0].Type, Is.EqualTo(UnionDataType.Array));
            Assert.That(ZipPayload.ByteCount(wire.Elements[0]), Is.LessThan(uncompressedSize),
                "the inner transport must observe a smaller byte stream than the uncompressed dataset");
        });

        var serverHandler = pair.ServerHandler;
        Assert.That(serverHandler, Is.Not.Null);
        Assert.That(serverHandler!.Received, Has.Count.EqualTo(1));
        Assert.That(serverHandler.Received[0], Is.EqualTo(new[] { "s:" + text }));
    }

    [Test]
    public void ReconnectAfterFirstSessionDisconnects_StillCompressesAndDecompresses()
    {
        using var pair = new ZipPair();
        pair.Start();

        var firstServerHandler = pair.ServerHandler;
        Assert.That(firstServerHandler, Is.Not.Null);

        string firstText = new string('A', 2000);
        Assert.That(pair.ClientHandler.Endpoint!.Send(ZipPayload.Create(firstText)),
            Is.EqualTo(SendResult.Ok));
        Assert.That(firstServerHandler!.Received, Has.Count.EqualTo(1));
        Assert.That(firstServerHandler.Received[0], Is.EqualTo(new[] { "s:" + firstText }));

        // The first session ends. The zip logic releases its compressor/decompressor in OnDisconnected,
        // so a shared server logic instance would leave every later session unable to decompress.
        pair.ServerSession!.Stop(new UserIntention("test", "first-session-disconnected"));
        pair.FirstClient.Stop();

        Assert.That(firstServerHandler.Events, Is.EqualTo(new[] { "connected", "disconnected", "stopped" }));

        var secondClient = pair.ConnectClient();
        var secondServerHandler = pair.ServerHandler;

        Assert.Multiple(() =>
        {
            Assert.That(secondServerHandler, Is.Not.Null);
            Assert.That(secondServerHandler, Is.Not.SameAs(firstServerHandler));
            Assert.That(pair.Factory.Calls, Is.EqualTo(2),
                "each session must be admitted with a fresh user handler");
        });

        string secondText = new string('B', 2000);
        UnionDataList payload = ZipPayload.Create(secondText);
        int uncompressedSize = payload.GetDataSize();

        Assert.That(secondClient.Handler.Endpoint!.Send(payload), Is.EqualTo(SendResult.Ok));

        Assert.Multiple(() =>
        {
            Assert.That(secondServerHandler!.ConnectedCount, Is.EqualTo(1));
            Assert.That(secondServerHandler.Received, Has.Count.EqualTo(1));
            Assert.That(secondServerHandler.Received[0], Is.EqualTo(new[] { "s:" + secondText }),
                "the later session must decompress to the original dataset");
        });

        var secondInnerEndpoint = secondClient.InnerClient.Endpoint;
        Assert.That(secondInnerEndpoint, Is.Not.Null);
        Assert.That(secondInnerEndpoint!.Sent, Has.Count.EqualTo(2));

        UnionDataList wire = secondInnerEndpoint.Sent[1];
        Assert.Multiple(() =>
        {
            Assert.That(wire.Elements, Has.Count.EqualTo(1),
                "the later session's payload reaches the inner transport compressed");
            Assert.That(wire.Elements[0].Type, Is.EqualTo(UnionDataType.Array));
            Assert.That(ZipPayload.ByteCount(wire.Elements[0]), Is.LessThan(uncompressedSize),
                "the later session still compresses its outbound payload");
        });
    }

    [Test]
    public void RawClientWithoutZipNegotiation_IsRejectedAndSkipsTheFactory()
    {
        var clock = new ManualZipClock();
        var innerServer = new FakeZipServer();
        var serverLogic = new RawReliableZipServerLogic(innerServer.Log, innerServer.Memory, 9);
        var server = new RawReliableWrapperServer("server", ZipInfo.TransportName, innerServer,
            serverLogic, 1, 128, null, clock);

        RecordingZipServerHandler? serverHandler = null;
        var factory = new ZipServerFactory(_ =>
        {
            serverHandler = new RecordingZipServerHandler();
            return serverHandler;
        });

        Assert.That(server.Init(factory), Is.True);
        Assert.That(server.Start(_ => { }), Is.True);

        FakeZipServerSession? session = null;
        var rawClient = new FakeZipClient();
        var rawHandler = new RecordingZipClientHandler();
        try
        {
            session = innerServer.Connect();

            Assert.That(rawClient.Init(rawHandler), Is.True);
            Assert.That(rawClient.Start(_ => { }), Is.True);
            rawClient.ConfigureEndpoint = endpoint =>
                endpoint.Deliver = data => session.DeliverToServer(data);
            session.Endpoint.Deliver = data => rawClient.DeliverToClient(data);
            rawClient.Connect();

            Assert.That(rawHandler.Endpoint!.Send(ZipPayload.Create("application-data")),
                Is.EqualTo(SendResult.Ok));

            Assert.Multiple(() =>
            {
                Assert.That(factory.Calls, Is.Zero,
                    "a payload that is not a zip negotiation request must not reach the user factory");
                Assert.That(serverHandler, Is.Null);
                Assert.That(session.Endpoint.Sent, Has.Count.EqualTo(1));
                Assert.That(ZipPayload.Describe(session.Endpoint.Sent[0]), Is.EqualTo(
                    new[] { "h:1", "s:zip", "b:2", "s:expected a negotiation request" }));
                Assert.That(rawHandler.Received, Has.Count.EqualTo(1));
                Assert.That(rawHandler.Received[0], Is.EqualTo(
                    new[] { "h:1", "s:zip", "b:2", "s:expected a negotiation request" }));
                Assert.That(clock.PendingDelays, Is.EqualTo(new[] { TimeSpan.FromSeconds(2) }));
            });
        }
        finally
        {
            server.Stop();
            rawClient.Endpoint?.ReleaseSent();
            session?.Release();
        }
    }

    [Test]
    public void VersionMismatch_StopsClientWithHandshakeRejectedAndSkipsFactory()
    {
        using var pair = new ZipPair(clientVersion: 2, serverVersion: 1);
        pair.Start();

        var serverHandler = pair.ServerHandler;

        Assert.Multiple(() =>
        {
            Assert.That(pair.Factory.Calls, Is.Zero);
            Assert.That(serverHandler, Is.Null);
            Assert.That(pair.ClientHandler.ConnectedCount, Is.Zero);
            Assert.That(pair.ClientHandler.Events, Is.EqualTo(new[] { "stopped" }));
            Assert.That(pair.ClientHandler.StopReason, Is.TypeOf<HandshakeRejected>());
            Assert.That(ZipPayload.Describe(pair.ServerSession!.Endpoint.Sent[0]), Is.EqualTo(
                new[] { "h:1", "s:zip", "b:2", "s:version mismatch" }));
            Assert.That(pair.Clock.PendingDelays, Is.EqualTo(new[] { TimeSpan.FromSeconds(2) }));
        });
    }

    [Test]
    public void NameMismatch_StopsClientWithHandshakeRejectedAndSkipsFactory()
    {
        using var pair = new ZipPair(clientName: "layer", serverName: ZipInfo.TransportName);
        pair.Start();

        var serverHandler = pair.ServerHandler;

        Assert.Multiple(() =>
        {
            Assert.That(pair.Factory.Calls, Is.Zero);
            Assert.That(serverHandler, Is.Null);
            Assert.That(pair.ClientHandler.ConnectedCount, Is.Zero);
            Assert.That(pair.ClientHandler.Events, Is.EqualTo(new[] { "stopped" }));
            Assert.That(pair.ClientHandler.StopReason, Is.TypeOf<HandshakeRejected>());
            Assert.That(ZipPayload.Describe(pair.ServerSession!.Endpoint.Sent[0]), Is.EqualTo(
                new[] { "h:1", "s:zip", "b:2", "s:name mismatch" }));
        });
    }
}
