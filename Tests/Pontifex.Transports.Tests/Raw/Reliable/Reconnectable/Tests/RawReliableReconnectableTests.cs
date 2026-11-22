using System;
using System.Collections.Generic;
using Actuarius.Memory;
using Pontifex.StopReasons;
using Pontifex.Tests.Raw.Reliable.Reconnectable.Harness;
using Pontifex.Utils;

namespace Pontifex.Tests.Raw.Reliable.Reconnectable;

/// <summary>
/// Contract tests for the reconnectable reliable wrapper: its own <c>"reconnectable"</c> negotiation
/// dataset, physical reconnection with session reattach, secret validation, and queued-message replay.
/// </summary>
[TestFixture]
[Category("Reconnectable")]
public sealed class RawReliableReconnectableTests
{
    [Test]
    public void FirstConnection_EstablishesSessionAndTransfersData()
    {
        using var pair = new ReconnectablePair();
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

        Assert.That(pair.ClientHandler.Endpoint!.Send(ReconnectablePayload.CreateInt(11)),
            Is.EqualTo(SendResult.Ok));
        pair.TickAll();
        Assert.That(serverHandler!.Received, Is.EqualTo(new[] { 11 }));

        Assert.That(serverHandler.Endpoint!.Send(ReconnectablePayload.CreateInt(22)),
            Is.EqualTo(SendResult.Ok));
        pair.TickAll();
        Assert.That(pair.ClientHandler.Received, Is.EqualTo(new[] { 22 }));
    }

    [Test]
    public void ReconnectWithSessionIdAndSecret_ReattachesExistingSession()
    {
        using var pair = new ReconnectablePair();
        pair.Start();

        var firstServerHandler = pair.ServerHandler;
        Assert.That(firstServerHandler, Is.Not.Null);
        Assert.That(pair.Factory.Calls, Is.EqualTo(1));

        pair.DropPhysicalConnection(new UserIntention("test", "fall"));

        Assert.Multiple(() =>
        {
            Assert.That(pair.ClientHandler.Events, Is.EqualTo(new[] { "connected" }),
                "a physical drop must not surface as an application disconnect");
            Assert.That(firstServerHandler!.Events, Is.EqualTo(new[] { "connected" }));
        });

        pair.Reconnect();

        Assert.Multiple(() =>
        {
            Assert.That(pair.Factory.Calls, Is.EqualTo(1),
                "reattach must not create a new user session");
            Assert.That(pair.ClientHandler.ConnectedCount, Is.EqualTo(1),
                "reattach must not raise a second application OnConnected");
            Assert.That(pair.ServerHandler, Is.SameAs(firstServerHandler));
            Assert.That(firstServerHandler!.ConnectedCount, Is.EqualTo(1));
        });

        Assert.That(pair.ClientHandler.Endpoint!.Send(ReconnectablePayload.CreateInt(33)),
            Is.EqualTo(SendResult.Ok));
        pair.TickAll();
        Assert.That(firstServerHandler!.Received, Is.EqualTo(new[] { 33 }),
            "data sent after reattach must reach the original server session");
    }

    [Test]
    public void WrongSecret_IsRejectedAndStopsTheInnerSessionExactlyOnce()
    {
        using var pair = new ReconnectablePair();
        pair.Start();

        var firstServerHandler = pair.ServerHandler;
        Assert.That(firstServerHandler, Is.Not.Null);

        pair.DropPhysicalConnection(new UserIntention("test", "fall"));
        pair.Reconnect(endpoint => endpoint.Transform = ReplaceLastArrayWithWrongSecret);

        var rejectedSession = pair.ServerSession;
        Assert.That(rejectedSession, Is.Not.Null);

        Assert.Multiple(() =>
        {
            Assert.That(pair.Factory.Calls, Is.EqualTo(1),
                "a failed reattach must not create a new user session");
            Assert.That(firstServerHandler!.Events, Is.EqualTo(new[] { "connected" }),
                "a failed reattach must not stop the existing session");
            Assert.That(pair.ClientHandler.StopReason, Is.TypeOf<HandshakeRejected>());
            Assert.That(pair.ClientHandler.Events, Is.EqualTo(
                new[] { "connected", "disconnected", "stopped" }));
        });

        Assert.That(rejectedSession!.HandlerStoppedCount, Is.Zero,
            "the rejected physical session waits for the linger before closing");

        pair.Clock.Advance(TimeSpan.FromSeconds(2));

        Assert.That(rejectedSession.HandlerStoppedCount, Is.EqualTo(1),
            "the rejected inner session must be stopped exactly once");
        Assert.That(rejectedSession.HandlerDisconnectedCount, Is.EqualTo(1));
    }

    [Test]
    public void VersionMismatch_IsRejectedWithoutOnConnectedOrFactory()
    {
        using var pair = new ReconnectablePair(clientVersion: 2);
        pair.Start();

        Assert.Multiple(() =>
        {
            Assert.That(pair.Factory.Calls, Is.Zero);
            Assert.That(pair.ServerHandler, Is.Null);
            Assert.That(pair.ClientHandler.ConnectedCount, Is.Zero);
            Assert.That(pair.ClientHandler.Events, Is.EqualTo(new[] { "stopped" }));
            Assert.That(pair.ClientHandler.StopReason, Is.TypeOf<HandshakeRejected>());
        });
    }

    [Test]
    public void NameMismatch_IsRejectedWithoutOnConnectedOrFactory()
    {
        using var pair = new ReconnectablePair(clientName: "not-reconnectable");
        pair.Start();

        Assert.Multiple(() =>
        {
            Assert.That(pair.Factory.Calls, Is.Zero);
            Assert.That(pair.ServerHandler, Is.Null);
            Assert.That(pair.ClientHandler.ConnectedCount, Is.Zero);
            Assert.That(pair.ClientHandler.Events, Is.EqualTo(new[] { "stopped" }));
            Assert.That(pair.ClientHandler.StopReason, Is.TypeOf<HandshakeRejected>());
        });
    }

    [Test]
    public void QueuedMessages_SurviveReattach()
    {
        using var pair = new ReconnectablePair();
        pair.Start();

        var serverHandler = pair.ServerHandler;
        Assert.That(serverHandler, Is.Not.Null);

        Assert.That(pair.ClientHandler.Endpoint!.Send(ReconnectablePayload.CreateInt(44)),
            Is.EqualTo(SendResult.Ok), "the queued message is accepted before the physical drop");
        Assert.That(serverHandler!.Received, Is.Empty);

        pair.DropPhysicalConnection(new UserIntention("test", "fall"));
        pair.Reconnect();

        pair.TickAll();

        Assert.That(pair.Factory.Calls, Is.EqualTo(1));
        Assert.That(serverHandler.Received, Is.EqualTo(new[] { 44 }),
            "a message queued before the drop must be replayed after reattach");
    }

    [Test]
    public void ReplayedMessageAfterReattach_IsDeliveredOnce()
    {
        using var pair = new ReconnectablePair();
        pair.Start();

        var serverHandler = pair.ServerHandler;
        Assert.That(serverHandler, Is.Not.Null);

        // A server-side send resets the report-flush window, so the delivery report
        // for the client's message is not emitted in the same tick.
        Assert.That(serverHandler!.Endpoint!.Send(ReconnectablePayload.CreateInt(1)),
            Is.EqualTo(SendResult.Ok));
        pair.TickAll();

        Assert.That(pair.ClientHandler.Endpoint!.Send(ReconnectablePayload.CreateInt(44)),
            Is.EqualTo(SendResult.Ok));
        pair.TickAll();

        Assert.That(serverHandler.Received, Is.EqualTo(new[] { 44 }),
            "the first transmission must reach the server before the drop");

        pair.DropPhysicalConnection(new UserIntention("test", "fall"));
        pair.Reconnect();
        pair.TickAll();

        Assert.That(pair.ServerSession!.ReceivedCount, Is.EqualTo(2),
            "the server must receive the reattach tail and one replayed message");
        Assert.That(serverHandler.Received, Is.EqualTo(new[] { 44 }),
            "a replayed unconfirmed message must be suppressed as a duplicate");
        Assert.That(pair.Factory.Calls, Is.EqualTo(1));
    }

    [Test]
    public void UnknownSessionId_IsRejectedWithoutFactoryCallOrSessionDamage()
    {
        using var pair = new ReconnectablePair();
        pair.Start();

        var firstHandler = pair.ServerHandler;
        Assert.That(firstHandler, Is.Not.Null);

        pair.DropPhysicalConnection(new UserIntention("test", "fall"));
        pair.Reconnect(endpoint => endpoint.Transform = input => OffsetReattachInt(0, 1000, input));

        var rejected = pair.ServerSession;
        Assert.Multiple(() =>
        {
            Assert.That(pair.Factory.Calls, Is.EqualTo(1),
                "an unknown session id must not create a new user session");
            Assert.That(firstHandler!.Events, Is.EqualTo(new[] { "connected" }),
                "the existing session must be untouched");
            Assert.That(pair.ClientHandler.StopReason, Is.TypeOf<HandshakeRejected>());
            Assert.That(pair.ClientHandler.Events,
                Is.EqualTo(new[] { "connected", "disconnected", "stopped" }));
            Assert.That(rejected!.HandlerStoppedCount, Is.Zero,
                "the rejected physical session waits for the linger");
        });

        pair.Clock.Advance(TimeSpan.FromSeconds(2));

        Assert.That(rejected!.HandlerStoppedCount, Is.EqualTo(1));
    }

    [Test]
    public void StaleGeneration_IsRejectedWithoutFactoryCallOrSessionDamage()
    {
        using var pair = new ReconnectablePair();
        pair.Start();

        var firstHandler = pair.ServerHandler;
        Assert.That(firstHandler, Is.Not.Null);

        pair.DropPhysicalConnection(new UserIntention("test", "fall"));
        pair.Reconnect(endpoint => endpoint.Transform = input => OffsetReattachInt(1, 1, input));

        var rejected = pair.ServerSession;
        Assert.Multiple(() =>
        {
            Assert.That(pair.Factory.Calls, Is.EqualTo(1));
            Assert.That(firstHandler!.Events, Is.EqualTo(new[] { "connected" }));
            Assert.That(pair.ClientHandler.StopReason, Is.TypeOf<HandshakeRejected>());
            Assert.That(pair.ClientHandler.Events,
                Is.EqualTo(new[] { "connected", "disconnected", "stopped" }));
        });

        pair.Clock.Advance(TimeSpan.FromSeconds(2));

        Assert.That(rejected!.HandlerStoppedCount, Is.EqualTo(1));
    }

    private static UnionDataList OffsetReattachInt(int index, int delta, UnionDataList input)
    {
        var elements = new List<UnionData>();
        while (input.TryPopFirst(out var element))
        {
            elements.Add(element);
        }

        input.Release();

        var result = MemoryRental.Shared.CollectablePool.Acquire<UnionDataList>();
        int intIndex = 0;
        foreach (var element in elements)
        {
            if (element.Type == UnionDataType.Int && intIndex++ == index)
            {
                result.PutLast(new UnionData(element.Alias.IntValue + delta));
            }
            else
            {
                result.PutLast(element);
            }
        }

        return result;
    }

    private static UnionDataList ReplaceLastArrayWithWrongSecret(UnionDataList input)
    {
        var elements = new List<UnionData>();
        while (input.TryPopFirst(out var element))
        {
            elements.Add(element);
        }

        input.Release();

        var result = MemoryRental.Shared.CollectablePool.Acquire<UnionDataList>();
        for (int i = 0; i < elements.Count - 1; i++)
        {
            result.PutLast(elements[i]);
        }

        elements[elements.Count - 1].Bytes?.Release();
        result.PutLast(new UnionData(new MultiRefByteArray(new byte[] { 1, 2, 3, 4 })));
        return result;
    }
}
