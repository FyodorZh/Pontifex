using Actuarius.Memory;
using Pontifex.Endpoints;
using Pontifex.Tests.Channels.Harness;
using Pontifex.Utils;
using Pontifex.VirtualDelivery;

namespace Pontifex.Tests.Channels;

/// <summary>
/// Pins the in-process channel pair: handler wiring, message ownership, disposal, and delivery-system
/// replacement.
/// </summary>
[TestFixture]
[Category("Direct.ChannelManager")]
public sealed class ChannelTests
{
    [Test]
    public void SendToClient_InvokesTheClientHandler()
    {
        using var channel = NewChannel(out _);
        UnionDataList? received = null;
        channel.ClientHandler = message => received = message;

        var message = NewMessage(5);
        var result = channel.SendToClient(message);

        try
        {
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo(SendResult.Ok));
                Assert.That(received, Is.SameAs(message));
                Assert.That(received!.Elements[0].Alias.IntValue, Is.EqualTo(5));
            });
        }
        finally
        {
            received?.Release();
        }
    }

    [Test]
    public void SendToServer_InvokesTheServerHandlerWithTheClientEndpoint()
    {
        using var channel = NewChannel(out var clientEndPoint);
        IEndPoint? seenEndPoint = null;
        UnionDataList? received = null;
        channel.ServerHandler = (endPoint, message) =>
        {
            seenEndPoint = endPoint;
            received = message;
        };

        var message = NewMessage(7);
        var result = channel.SendToServer(message);

        try
        {
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo(SendResult.Ok));
                Assert.That(seenEndPoint, Is.SameAs(clientEndPoint));
                Assert.That(received, Is.SameAs(message));
            });
        }
        finally
        {
            received?.Release();
        }
    }

    [Test]
    public void SendToClient_WithoutAHandler_ReleasesTheMessage()
    {
        using var channel = NewChannel(out _);
        var message = NewMessage(1);

        var result = channel.SendToClient(message);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(SendResult.Ok));
            Assert.That(message.IsAlive, Is.False);
        });
    }

    [Test]
    public void SendToServer_WithoutAHandler_ReleasesTheMessage()
    {
        using var channel = NewChannel(out _);
        var message = NewMessage(1);

        var result = channel.SendToServer(message);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(SendResult.Ok));
            Assert.That(message.IsAlive, Is.False);
        });
    }

    [Test]
    public void SendToClient_AfterDispose_ReturnsNotConnectedAndReleasesTheMessage()
    {
        var channel = NewChannel(out _);
        channel.Dispose();
        var message = NewMessage(1);

        var result = channel.SendToClient(message);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(SendResult.NotConnected));
            Assert.That(message.IsAlive, Is.False);
        });
    }

    [Test]
    public void SendToServer_AfterDispose_ReturnsNotConnectedAndReleasesTheMessage()
    {
        var channel = NewChannel(out _);
        channel.Dispose();
        var message = NewMessage(1);

        var result = channel.SendToServer(message);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(SendResult.NotConnected));
            Assert.That(message.IsAlive, Is.False);
        });
    }

    [Test]
    public void Dispose_IsIdempotentAndRaisesTheEventOnce()
    {
        var channel = NewChannel(out _);
        int disposedCount = 0;
        channel.Disposed += () => disposedCount++;

        channel.Dispose();
        channel.Dispose();

        Assert.Multiple(() =>
        {
            Assert.That(disposedCount, Is.EqualTo(1));
            Assert.That(channel.IsDisposed, Is.True);
        });
    }

    [Test]
    public void Dispose_ContinuesWhenASubscriberThrows()
    {
        var channel = NewChannel(out _);
        int secondSubscriberCount = 0;
        channel.Disposed += () => throw new InvalidOperationException("subscriber fault");
        channel.Disposed += () => secondSubscriberCount++;

        Assert.DoesNotThrow(() => channel.Dispose());
        Assert.That(secondSubscriberCount, Is.EqualTo(1));
    }

    [Test]
    public void Messages_AreDeliveredInSendOrder()
    {
        using var channel = NewChannel(out _);
        var receivedValues = new List<int>();
        channel.ClientHandler = message =>
        {
            try
            {
                receivedValues.Add(message.Elements[0].Alias.IntValue);
            }
            finally
            {
                message.Release();
            }
        };

        for (int i = 0; i < 5; i++)
        {
            Assert.That(channel.SendToClient(NewMessage(i)), Is.EqualTo(SendResult.Ok));
        }

        Assert.That(receivedValues, Is.EqualTo(new[] { 0, 1, 2, 3, 4 }));
    }

    [Test]
    public void ClientEndPoint_IsTheConfiguredInstance()
    {
        using var channel = NewChannel(out var clientEndPoint);

        Assert.That(channel.ClientEp, Is.SameAs(clientEndPoint));
    }

    [Test]
    public void ReplacingTheClientDeliverySystem_ReleasesTheOldSystem()
    {
        using var channel = NewChannel(out _);
        var first = new TrackingDeliverySystem();

        channel.SetClientDeliverySystem(first);
        channel.SetClientDeliverySystem(new PerfectDeliverySystem());

        Assert.Multiple(() =>
        {
            Assert.That(first.ClearCount, Is.EqualTo(1));
            Assert.That(first.DisposeCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void ReplacingTheServerDeliverySystem_ReleasesTheOldSystem()
    {
        using var channel = NewChannel(out _);
        var first = new TrackingDeliverySystem();

        channel.SetServerDeliverySystem(first);
        channel.SetServerDeliverySystem(new PerfectDeliverySystem());

        Assert.Multiple(() =>
        {
            Assert.That(first.ClearCount, Is.EqualTo(1));
            Assert.That(first.DisposeCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void DisposingTheChannel_ReleasesBothDeliverySystems()
    {
        var channel = NewChannel(out _);
        var client = new TrackingDeliverySystem();
        var server = new TrackingDeliverySystem();

        channel.SetClientDeliverySystem(client);
        channel.SetServerDeliverySystem(server);
        channel.Dispose();

        Assert.Multiple(() =>
        {
            Assert.That(client.ClearCount, Is.EqualTo(1));
            Assert.That(client.DisposeCount, Is.EqualTo(1));
            Assert.That(server.ClearCount, Is.EqualTo(1));
            Assert.That(server.DisposeCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void SettingTheSameClientDeliverySystem_DoesNotReleaseIt()
    {
        using var channel = NewChannel(out _);
        var system = new TrackingDeliverySystem();

        channel.SetClientDeliverySystem(system);
        channel.SetClientDeliverySystem(system);

        Assert.Multiple(() =>
        {
            Assert.That(system.ClearCount, Is.Zero);
            Assert.That(system.DisposeCount, Is.Zero);
        });
    }

    private static Channel NewChannel(out IEndPoint clientEndPoint)
    {
        clientEndPoint = new StringEndPoint("client-" + Guid.NewGuid().ToString("N"));
        return new Channel(clientEndPoint, new StringEndPoint("server-" + Guid.NewGuid().ToString("N")));
    }

    private static UnionDataList NewMessage(int value)
    {
        var message = MemoryRental.Shared.CollectablePool.Acquire<UnionDataList>();
        message.PutLast(value);
        return message;
    }
}
