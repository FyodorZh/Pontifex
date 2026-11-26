using Actuarius.Memory;
using Pontifex.Raw.Reliable;
using Pontifex.Raw.Reliable.Protocols;
using Pontifex.StopReasons;
using Pontifex.Tests.Raw.Reliable.Protocol.Harness;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Tests.Raw.Reliable.Protocol;

/// <summary>
/// Deterministic tests for the reusable reliable-wrapper negotiation base: the flat envelope, the admission
/// gate, custom tails, timeouts, rejection linger, and the terminal lifecycle.
/// </summary>
[TestFixture]
[Category("Protocol")]
public sealed class RawReliableWrapperTests
{
    private static ILogger Log => new Logger([]);

    [Test]
    public void AcceptedNegotiation_AdmitsBothSidesAndForwardsDataInBothDirections()
    {
        using var pair = new WrapperPair();
        pair.Start();

        Assert.Multiple(() =>
        {
            Assert.That(pair.ClientHandler.ConnectedCount, Is.EqualTo(1));
            Assert.That(pair.Factory.Calls, Is.EqualTo(1));
            Assert.That(pair.ServerHandler, Is.Not.Null);
            Assert.That(pair.ServerHandler!.ConnectedCount, Is.EqualTo(1));
            Assert.That(pair.ClientHandler.Endpoint!.IsConnected, Is.True);
            Assert.That(pair.ServerHandler.Endpoint!.IsConnected, Is.True);
            Assert.That(pair.ClientHandler.Endpoint.MessageMaxByteSize, Is.EqualTo(4096));
        });

        Assert.That(Payload.Describe(pair.InnerClient.Endpoint!.Sent[0]), Is.EqualTo(
            new[] { "h:1", "s:layer", "b:0" }));
        Assert.That(Payload.Describe(pair.ServerSession!.Endpoint.Sent[0]), Is.EqualTo(
            new[] { "h:1", "s:layer", "b:1" }));

        Assert.That(pair.ClientHandler.Endpoint!.Send(Payload.Create("hello")), Is.EqualTo(SendResult.Ok));
        Assert.That(pair.ServerHandler!.Received, Has.Count.EqualTo(1));
        Assert.That(pair.ServerHandler.Received[0], Is.EqualTo(new[] { "s:hello" }));

        Assert.That(pair.ServerHandler.Endpoint!.Send(Payload.Create("world")), Is.EqualTo(SendResult.Ok));
        Assert.That(pair.ClientHandler.Received, Has.Count.EqualTo(1));
        Assert.That(pair.ClientHandler.Received[0], Is.EqualTo(new[] { "s:world" }));
    }

    [Test]
    public void Client_WhenProcessReceivedDataReturnsFalse_StopsWithTextFail()
    {
        using var pair = new WrapperPair();
        pair.ClientLogic.ProcessReceivedResult = false;
        pair.Start();

        Assert.That(pair.ServerHandler!.Endpoint!.Send(Payload.Create("x")), Is.EqualTo(SendResult.Ok));

        Assert.Multiple(() =>
        {
            Assert.That(pair.ClientHandler.ReceivedCount, Is.Zero,
                "a rejected payload must not reach the application handler");
            Assert.That(pair.ClientHandler.Events,
                Is.EqualTo(new[] { "connected", "disconnected", "stopped" }));
            Assert.That(pair.ClientHandler.StopReason, Is.TypeOf<TextFail>());
            Assert.That(pair.ClientHandler.Endpoint!.IsConnected, Is.False);
        });
    }

    [Test]
    public void Server_WhenProcessReceivedDataReturnsFalse_DisconnectsWithTextFail()
    {
        using var pair = new WrapperPair();
        pair.ServerLogic.ProcessReceivedResult = false;
        pair.Start();

        Assert.That(pair.ClientHandler.Endpoint!.Send(Payload.Create("x")), Is.EqualTo(SendResult.Ok));

        Assert.Multiple(() =>
        {
            Assert.That(pair.ServerHandler!.ReceivedCount, Is.Zero,
                "a rejected payload must not reach the application handler");
            Assert.That(pair.ServerHandler.Events,
                Is.EqualTo(new[] { "connected", "disconnected", "stopped" }));
            Assert.That(pair.ServerHandler.StopReason, Is.TypeOf<TextFail>());
            Assert.That(pair.ServerSession!.Endpoint.IsConnected, Is.False);
        });
    }

    [Test]
    public void ServerHandshakeTimeout_RejectsWithTimeoutTextAndStopsAfterLinger()
    {
        var clock = new ManualTimerFactory();
        var log = new Logger([]);
        var logic = new RecordingWrapperLogic(log, MemoryRental.Shared);
        var inner = new FakeReliableServer();
        var server = new RawReliableWrapperServer("server", "layer", inner, logic, 1, 128, null, clock);
        var factory = new RecordingServerFactory(_ => new RecordingRawReliableServerHandler());

        Assert.That(server.Init(factory), Is.True);
        Assert.That(server.Start(_ => { }), Is.True);

        var session = inner.Connect();

        Assert.That(clock.PendingDelays, Is.EqualTo(new[] { TimeSpan.FromSeconds(10) }));
        Assert.That(factory.Calls, Is.Zero);

        clock.Advance(TimeSpan.FromSeconds(10));

        Assert.Multiple(() =>
        {
            Assert.That(factory.Calls, Is.Zero, "a timed-out session must never reach the factory");
            Assert.That(session.Endpoint.Sent, Has.Count.EqualTo(1));
            Assert.That(Payload.Describe(session.Endpoint.Sent[0]), Is.EqualTo(
                new[] { "h:1", "s:layer", "b:2", "s:negotiation timeout" }));
            Assert.That(session.Endpoint.IsConnected, Is.True,
                "the rejection linger keeps the inner session open");
            Assert.That(clock.PendingDelays, Is.EqualTo(new[] { TimeSpan.FromSeconds(2) }));
            Assert.That(logic.ConnectedCount, Is.Zero);
        });

        clock.Advance(TimeSpan.FromSeconds(2));

        Assert.Multiple(() =>
        {
            Assert.That(session.Endpoint.IsConnected, Is.False);
            Assert.That(clock.PendingCount, Is.Zero);
        });
    }

    [Test]
    public void VersionMismatch_StopsClientWithoutOnConnected()
    {
        var client = new FakeReliableClient();
        var logic = new RecordingWrapperLogic(Log, MemoryRental.Shared);
        var handler = new RecordingRawReliableClientHandler();
        var wrapper = new RawReliableWrapperClient("client", "layer", client, logic, 1, 128, null,
            new ManualTimerFactory());

        Assert.That(wrapper.Init(handler), Is.True);
        Assert.That(wrapper.Start(_ => { }), Is.True);
        client.Connect();

        client.DeliverToClient(RawReliableNegotiation.BuildResponse(2, "layer", null));

        Assert.Multiple(() =>
        {
            Assert.That(handler.ConnectedCount, Is.Zero);
            Assert.That(handler.Events, Is.EqualTo(new[] { "stopped" }));
            Assert.That(handler.StopReason, Is.TypeOf<ProtocolMismatch>());
            Assert.That(client.IsStarted, Is.False);
        });
    }

    [Test]
    public void NameMismatch_StopsClientWithoutOnConnected()
    {
        var client = new FakeReliableClient();
        var logic = new RecordingWrapperLogic(Log, MemoryRental.Shared);
        var handler = new RecordingRawReliableClientHandler();
        var wrapper = new RawReliableWrapperClient("client", "layer", client, logic, 1, 128, null,
            new ManualTimerFactory());

        Assert.That(wrapper.Init(handler), Is.True);
        Assert.That(wrapper.Start(_ => { }), Is.True);
        client.Connect();

        client.DeliverToClient(RawReliableNegotiation.BuildResponse(1, "other-layer", null));

        Assert.Multiple(() =>
        {
            Assert.That(handler.ConnectedCount, Is.Zero);
            Assert.That(handler.Events, Is.EqualTo(new[] { "stopped" }));
            Assert.That(handler.StopReason, Is.TypeOf<ProtocolMismatch>());
        });
    }

    [Test]
    public void HandshakeTimeout_StopsClientWithoutOnConnected()
    {
        var clock = new ManualTimerFactory();
        var client = new FakeReliableClient();
        var logic = new RecordingWrapperLogic(Log, MemoryRental.Shared);
        var handler = new RecordingRawReliableClientHandler();
        var wrapper = new RawReliableWrapperClient("client", "layer", client, logic, 1, 128, null, clock);

        Assert.That(wrapper.Init(handler), Is.True);
        Assert.That(wrapper.Start(_ => { }), Is.True);
        client.Connect();

        Assert.That(clock.PendingDelays, Is.EqualTo(new[] { TimeSpan.FromSeconds(10) }));

        clock.Advance(TimeSpan.FromSeconds(10));

        Assert.Multiple(() =>
        {
            Assert.That(handler.ConnectedCount, Is.Zero);
            Assert.That(handler.Events, Is.EqualTo(new[] { "stopped" }));
            Assert.That(handler.StopReason, Is.TypeOf<TimeOut>());
            Assert.That(clock.PendingCount, Is.Zero);
        });
    }

    [Test]
    public void RejectLinger_IsExactlyTwoSecondsByDefault()
    {
        using var defaultSession = new NegotiationSession(
            NegotiationRole.Server,
            1,
            "layer",
            frame =>
            {
                frame.Release();
                return SendResult.Ok;
            },
            _ => { },
            _ => { },
            _ => { },
            checkData: _ => false);

        Assert.That(defaultSession.RejectLinger, Is.EqualTo(TimeSpan.FromSeconds(2)));

        var clock = new ManualTimerFactory();
        var sent = new List<UnionDataList>();
        var disconnects = new List<StopReason>();
        StopReason? rejection = null;

        using var session = new NegotiationSession(
            NegotiationRole.Server,
            1,
            "layer",
            frame =>
            {
                sent.Add(frame);
                return SendResult.Ok;
            },
            reason => rejection = reason,
            reason => disconnects.Add(reason),
            _ => { },
            checkData: _ => false,
            timerFactory: clock,
            log: Log);

        session.Start();
        UnionDataList request = RawReliableNegotiation.BuildRequest(1, "layer", null);
        Assert.That(session.OnReceived(request), Is.True);

        Assert.Multiple(() =>
        {
            Assert.That(sent, Has.Count.EqualTo(1));
            Assert.That(Payload.Describe(sent[0]), Is.EqualTo(
                new[] { "h:1", "s:layer", "b:2", "s:rejected" }));
            Assert.That(rejection, Is.TypeOf<HandshakeRejected>());
            Assert.That(clock.PendingDelays, Is.EqualTo(new[] { TimeSpan.FromSeconds(2) }));
            Assert.That(disconnects, Is.Empty);
        });

        clock.Advance(TimeSpan.FromSeconds(2));

        Assert.That(disconnects, Has.Count.EqualTo(1));

        sent[0].Release();
    }

    [Test]
    public void PayloadBeforeAdmission_IsNeverDeliveredToUserHandler()
    {
        var client = new FakeReliableClient();
        var logic = new RecordingWrapperLogic(Log, MemoryRental.Shared);
        var handler = new RecordingRawReliableClientHandler();
        var wrapper = new RawReliableWrapperClient("client", "layer", client, logic, 1, 128, null,
            new ManualTimerFactory());

        Assert.That(wrapper.Init(handler), Is.True);
        Assert.That(wrapper.Start(_ => { }), Is.True);
        client.Connect();

        client.DeliverToClient(Payload.Create("application-data"));

        Assert.Multiple(() =>
        {
            Assert.That(handler.ReceivedCount, Is.Zero);
            Assert.That(handler.ConnectedCount, Is.Zero);
            Assert.That(handler.Events, Is.EqualTo(new[] { "stopped" }));
            Assert.That(handler.StopReason, Is.TypeOf<ProtocolMismatch>());
        });
    }

    [Test]
    public void SendBeforeAdmission_IsNotRoutedToInnerTransport()
    {
        var client = new FakeReliableClient();
        var logic = new RecordingWrapperLogic(Log, MemoryRental.Shared);
        var handler = new RecordingRawReliableClientHandler();
        var wrapper = new RawReliableWrapperClient("client", "layer", client, logic, 1, 128, null,
            new ManualTimerFactory());

        Assert.That(wrapper.Init(handler), Is.True);
        Assert.That(wrapper.Start(_ => { }), Is.True);
        client.Connect();

        var proxy = (IRawReliableEndpoint)client.Handler!;
        UnionDataList payload = Payload.Create("too-early");
        SendResult result = proxy.Send(payload);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(SendResult.NotConnected));
            Assert.That(payload.IsAlive, Is.False);
            Assert.That(client.Endpoint!.Sent, Has.Count.EqualTo(1));
            Assert.That(handler.ConnectedCount, Is.Zero);
        });

        client.DeliverToClient(RawReliableNegotiation.BuildResponse(1, "layer", null));
        Assert.That(handler.ConnectedCount, Is.EqualTo(1));
    }

    [Test]
    public void CustomTail_RoundTripsFromClientToServerAndBack()
    {
        var clientLogic = new RecordingWrapperLogic(Log, MemoryRental.Shared)
        {
            Fill = tail => tail.PutLast(new UnionData(new MultiRefByteArray("k"u8.ToArray())))
        };
        var serverLogic = new RecordingWrapperLogic(Log, MemoryRental.Shared)
        {
            ResponseTail = Payload.Create("v")
        };

        using var pair = new WrapperPair(clientLogic: clientLogic, serverLogic: serverLogic);
        pair.Start();

        Assert.Multiple(() =>
        {
            Assert.That(serverLogic.CheckedTails, Has.Count.EqualTo(1));
            Assert.That(serverLogic.CheckedTails[0], Is.EqualTo(new[] { "s:k" }));
            Assert.That(clientLogic.AcceptedTails, Has.Count.EqualTo(1));
            Assert.That(clientLogic.AcceptedTails[0], Is.EqualTo(new[] { "s:v" }));
            Assert.That(pair.ClientHandler.ConnectedCount, Is.EqualTo(1));
            Assert.That(pair.ServerHandler!.ConnectedCount, Is.EqualTo(1));
            Assert.That(Payload.Describe(pair.InnerClient.Endpoint!.Sent[0]), Is.EqualTo(
                new[] { "h:1", "s:layer", "b:0", "s:k" }));
            Assert.That(Payload.Describe(pair.ServerSession!.Endpoint.Sent[0]), Is.EqualTo(
                new[] { "h:1", "s:layer", "b:1", "s:v" }));
        });
    }

    [Test]
    public void ServerVersionMismatch_RejectsClientSkipsFactoryAndDisconnectsAfterLinger()
    {
        using var pair = new WrapperPair(clientVersion: 2);
        pair.Start();

        Assert.Multiple(() =>
        {
            Assert.That(pair.ClientHandler.ConnectedCount, Is.Zero);
            Assert.That(pair.ClientHandler.Events, Is.EqualTo(new[] { "stopped" }));
            Assert.That(pair.ClientHandler.StopReason, Is.TypeOf<HandshakeRejected>());
            Assert.That(pair.Factory.Calls, Is.Zero);
            Assert.That(pair.ServerHandler, Is.Null);
            Assert.That(Payload.Describe(pair.ServerSession!.Endpoint.Sent[0]), Is.EqualTo(
                new[] { "h:1", "s:layer", "b:2", "s:version mismatch" }));
            Assert.That(pair.Clock.PendingDelays, Is.EqualTo(new[] { TimeSpan.FromSeconds(2) }));
        });

        pair.Clock.Advance(TimeSpan.FromSeconds(2));
        Assert.That(pair.ServerSession!.Endpoint.Connected, Is.False,
            "the linger must disconnect the inner session exactly once");
        Assert.That(pair.Clock.PendingCount, Is.Zero);
    }

    [Test]
    public void ServerNameMismatch_RejectsClientSkipsFactory()
    {
        using var pair = new WrapperPair(clientName: "other");
        pair.Start();

        Assert.Multiple(() =>
        {
            Assert.That(pair.ClientHandler.ConnectedCount, Is.Zero);
            Assert.That(pair.ClientHandler.Events, Is.EqualTo(new[] { "stopped" }));
            Assert.That(pair.ClientHandler.StopReason, Is.TypeOf<HandshakeRejected>());
            Assert.That(pair.Factory.Calls, Is.Zero);
            Assert.That(pair.ServerHandler, Is.Null);
            Assert.That(Payload.Describe(pair.ServerSession!.Endpoint.Sent[0]), Is.EqualTo(
                new[] { "h:1", "s:layer", "b:2", "s:name mismatch" }));
        });
    }

    [Test]
    public void CustomTailRejection_StopsClientWithHandshakeRejectedAndSkipsFactory()
    {
        var serverLogic = new RecordingWrapperLogic(Log, MemoryRental.Shared) { CheckResult = false };
        using var pair = new WrapperPair(serverLogic: serverLogic);
        pair.Start();

        Assert.Multiple(() =>
        {
            Assert.That(pair.ClientHandler.ConnectedCount, Is.Zero);
            Assert.That(pair.ClientHandler.Events, Is.EqualTo(new[] { "stopped" }));
            Assert.That(pair.ClientHandler.StopReason, Is.TypeOf<HandshakeRejected>());
            Assert.That(pair.Factory.Calls, Is.Zero);
            Assert.That(pair.ServerHandler, Is.Null);
            Assert.That(pair.Server.IsStarted, Is.True);
            Assert.That(pair.Clock.PendingDelays, Is.EqualTo(new[] { TimeSpan.FromSeconds(2) }));
        });

        pair.Server.Stop();
        Assert.That(pair.Clock.PendingCount, Is.Zero);
    }

    [Test]
    public void CustomTailExceedingHandshakeMaxByteSize_IsRejectedNotTruncated()
    {
        var logic = new RecordingWrapperLogic(Log, MemoryRental.Shared)
        {
            Fill = tail => tail.PutLast(new UnionData(new MultiRefByteArray("012345678901"u8.ToArray())))
        };
        var client = new FakeReliableClient();
        var handler = new RecordingRawReliableClientHandler();
        var wrapper = new RawReliableWrapperClient("client", "layer", client, logic, 1, 16, null,
            new ManualTimerFactory());

        Assert.That(wrapper.Init(handler), Is.True);
        Assert.That(wrapper.Start(_ => { }), Is.True);
        client.Connect();

        Assert.Multiple(() =>
        {
            Assert.That(client.Endpoint!.Sent, Is.Empty);
            Assert.That(handler.ConnectedCount, Is.Zero);
            Assert.That(handler.Events, Is.EqualTo(new[] { "stopped" }));
            Assert.That(handler.StopReason, Is.TypeOf<ProtocolMismatch>());
        });
    }

    [Test]
    public void ServerResponseTailExceedingMax_IsRejectedAndNeverDeliveredAsAccepted()
    {
        var serverLogic = new RecordingWrapperLogic(Log, MemoryRental.Shared)
        {
            ResponseTail = Payload.Create(new string('x', 100))
        };
        using var pair = new WrapperPair(serverMaxByteSize: 40, clientMaxByteSize: 40,
            serverLogic: serverLogic);
        pair.Start();

        Assert.Multiple(() =>
        {
            Assert.That(pair.Factory.Calls, Is.EqualTo(1));
            Assert.That(pair.ServerHandler, Is.Not.Null);
            Assert.That(pair.ServerHandler!.ConnectedCount, Is.Zero);
            Assert.That(pair.ServerHandler.Events, Is.EqualTo(new[] { "stopped" }));
            Assert.That(pair.ClientHandler.ConnectedCount, Is.Zero);
            Assert.That(pair.ClientHandler.StopReason, Is.TypeOf<HandshakeRejected>());
            Assert.That(Payload.Describe(pair.ServerSession!.Endpoint.Sent[0]), Is.EqualTo(
                new[] { "h:1", "s:layer", "b:2", "s:response too large" }));
            Assert.That(pair.Clock.PendingDelays, Is.EqualTo(new[] { TimeSpan.FromSeconds(2) }));
        });

        pair.Server.Stop();
        Assert.That(pair.Clock.PendingCount, Is.Zero);
    }

    [Test]
    public void CustomIntTailThatLooksLikeAStatus_IsParsedAsCustomData()
    {
        var clientLogic = new RecordingWrapperLogic(Log, MemoryRental.Shared)
        {
            Fill = tail => tail.PutLast(new UnionData(2))
        };
        var serverLogic = new RecordingWrapperLogic(Log, MemoryRental.Shared);

        using var pair = new WrapperPair(clientLogic: clientLogic, serverLogic: serverLogic);
        pair.Start();

        Assert.Multiple(() =>
        {
            Assert.That(serverLogic.CheckedTails, Has.Count.EqualTo(1));
            Assert.That(serverLogic.CheckedTails[0], Is.EqualTo(new[] { "i:2" }));
            Assert.That(pair.ClientHandler.ConnectedCount, Is.EqualTo(1));
            Assert.That(pair.ServerHandler!.ConnectedCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void AdmittedClientSession_ReceivesConnectedDisconnectedStoppedExactlyOnce()
    {
        var client = new FakeReliableClient();
        var logic = new RecordingWrapperLogic(Log, MemoryRental.Shared);
        var handler = new RecordingRawReliableClientHandler();
        var wrapper = new RawReliableWrapperClient("client", "layer", client, logic, 1, 128, null,
            new ManualTimerFactory());

        Assert.That(wrapper.Init(handler), Is.True);
        Assert.That(wrapper.Start(_ => { }), Is.True);
        client.Connect();
        client.DeliverToClient(RawReliableNegotiation.BuildResponse(1, "layer", null));

        var reason = new UserIntention("test", "disconnect");
        client.DisconnectFromRemote(reason);

        Assert.Multiple(() =>
        {
            Assert.That(handler.Events, Is.EqualTo(new[] { "connected", "disconnected", "stopped" }));
            Assert.That(handler.DisconnectReason, Is.SameAs(reason));
            Assert.That(handler.StopReason, Is.SameAs(reason));
            Assert.That(logic.ConnectedCount, Is.EqualTo(1));
            Assert.That(logic.DisconnectedCount, Is.EqualTo(1));
        });

        wrapper.Stop(new UserIntention("test", "again"));

        Assert.That(handler.Events, Is.EqualTo(new[] { "connected", "disconnected", "stopped" }));
    }

    [Test]
    public void NeverConnectedClientSession_ReceivesExactlyOneStoppedAndNoDisconnected()
    {
        var client = new FakeReliableClient();
        var logic = new RecordingWrapperLogic(Log, MemoryRental.Shared);
        var handler = new RecordingRawReliableClientHandler();
        var wrapper = new RawReliableWrapperClient("client", "layer", client, logic, 1, 128, null,
            new ManualTimerFactory());

        Assert.That(wrapper.Init(handler), Is.True);
        Assert.That(wrapper.Start(_ => { }), Is.True);
        client.Connect();

        wrapper.Stop(new UserIntention("test", "stop"));

        Assert.Multiple(() =>
        {
            Assert.That(handler.Events, Is.EqualTo(new[] { "stopped" }));
            Assert.That(handler.DisconnectReason, Is.Null);
            Assert.That(handler.ConnectedCount, Is.Zero);
        });
    }

    [Test]
    public void AdmittedServerSession_ReceivesConnectedDisconnectedStoppedExactlyOnce()
    {
        using var pair = new WrapperPair();
        pair.Start();

        var reason = new UserIntention("test", "session-stop");
        pair.ServerSession!.Stop(reason);

        Assert.Multiple(() =>
        {
            Assert.That(pair.ServerHandler, Is.Not.Null);
            Assert.That(pair.ServerHandler!.Events,
                Is.EqualTo(new[] { "connected", "disconnected", "stopped" }));
            Assert.That(pair.ServerHandler.DisconnectReason, Is.SameAs(reason));
            Assert.That(pair.ServerHandler.StopReason, Is.SameAs(reason));
            Assert.That(pair.ServerLogic.ConnectedCount, Is.EqualTo(1));
            Assert.That(pair.ServerLogic.DisconnectedCount, Is.EqualTo(1));
        });

        pair.Server.Stop();

        Assert.That(pair.ServerHandler.Events,
            Is.EqualTo(new[] { "connected", "disconnected", "stopped" }));
    }
}
