using Actuarius.Memory;
using Pontifex.Endpoints;
using Pontifex.Utils;

namespace Pontifex.Tests.Channels;

/// <summary>
/// Pins the process-wide channel registry: server registration, client pairing, removal, detachment, and
/// equality-based addressing.
/// </summary>
[TestFixture]
[Category("Direct.ChannelManager")]
public sealed class ChannelManagerTests
{
    private readonly List<IEndPoint> _servers = new();
    private readonly List<Channel> _created = new();
    private readonly object _createdLock = new();

    [TearDown]
    public void TearDown()
    {
        foreach (var server in _servers)
        {
            ChannelManager.Instance.UnregisterServer(server);
        }

        _servers.Clear();
        lock (_createdLock)
        {
            _created.Clear();
        }
    }

    [Test]
    public void RegisterServer_WithAUniqueEndPoint_ReturnsTrue()
    {
        var server = NewServerEndPoint();

        Assert.That(ChannelManager.Instance.RegisterServer(server, _ => { }), Is.True);
        _servers.Add(server);
    }

    [Test]
    public void RegisterServer_WithADuplicateEndPoint_ReturnsFalse()
    {
        var server = Register();

        Assert.That(ChannelManager.Instance.RegisterServer(server, _ => { }), Is.False);
    }

    [Test]
    public void RegisterServer_WithAnEqualEndPoint_ReturnsFalse()
    {
        var name = "cm-dup-" + Guid.NewGuid().ToString("N");
        var server = new StringEndPoint(name);
        Assert.That(ChannelManager.Instance.RegisterServer(server, _ => { }), Is.True);
        _servers.Add(server);
        var equal = new StringEndPoint(name);

        Assert.That(ChannelManager.Instance.RegisterServer(equal, _ => { }), Is.False);
    }

    [Test]
    public void Connect_WithoutARegisteredServer_ReturnsNull()
    {
        var channel = ChannelManager.Instance.Connect(NewServerEndPoint(), NewClientEndPoint());

        Assert.That(channel, Is.Null);
    }

    [Test]
    public void Connect_ToARegisteredServer_ReturnsAChannelAndInvokesTheCallback()
    {
        Channel? created = null;
        var server = Register(channel => created = channel);
        var client = NewClientEndPoint();

        var channel = ChannelManager.Instance.Connect(server, client);

        Assert.Multiple(() =>
        {
            Assert.That(channel, Is.Not.Null);
            Assert.That(created, Is.SameAs(channel));
            Assert.That(channel!.ClientEp, Is.SameAs(client));
        });
    }

    [Test]
    public void Connect_WithADuplicateClientEndPoint_ReturnsNullAndKeepsOneChannel()
    {
        var server = Register();
        var client = NewClientEndPoint();

        var first = ChannelManager.Instance.Connect(server, client);
        var second = ChannelManager.Instance.Connect(server, client);

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.Not.Null);
            Assert.That(second, Is.Null);
            Assert.That(CreatedCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void Disconnect_RemovesAndDisposesTheChannel()
    {
        var server = Register();
        var client = NewClientEndPoint();
        var channel = ChannelManager.Instance.Connect(server, client);

        Assert.That(channel, Is.Not.Null);
        Assert.That(ChannelManager.Instance.Disconnect(server, client), Is.True);
        Assert.That(channel!.IsDisposed, Is.True);
        Assert.That(ChannelManager.Instance.Disconnect(server, client), Is.False);
    }

    [Test]
    public void Disconnect_WithAnUnknownClient_ReturnsFalse()
    {
        var server = Register();

        Assert.That(ChannelManager.Instance.Disconnect(server, NewClientEndPoint()), Is.False);
    }

    [Test]
    public void Detach_RemovesWithoutDisposingTheChannel()
    {
        var server = Register();
        var client = NewClientEndPoint();
        var channel = ChannelManager.Instance.Connect(server, client);

        Assert.That(channel, Is.Not.Null);
        Assert.That(ChannelManager.Instance.Detach(server, client), Is.True);
        Assert.That(channel!.IsDisposed, Is.False);
        Assert.That(ChannelManager.Instance.Disconnect(server, client), Is.False);

        var replacement = ChannelManager.Instance.Connect(server, client);
        Assert.That(replacement, Is.Not.Null);

        replacement!.Dispose();
        channel.Dispose();
    }

    [Test]
    public void Detach_WithAnUnknownClient_ReturnsFalse()
    {
        var server = Register();

        Assert.That(ChannelManager.Instance.Detach(server, NewClientEndPoint()), Is.False);
    }

    [Test]
    public void UnregisterServer_DisposesEveryConnectedChannel()
    {
        var server = Register();
        var first = ChannelManager.Instance.Connect(server, NewClientEndPoint());
        var second = ChannelManager.Instance.Connect(server, NewClientEndPoint());

        ChannelManager.Instance.UnregisterServer(server);

        Assert.Multiple(() =>
        {
            Assert.That(first!.IsDisposed, Is.True);
            Assert.That(second!.IsDisposed, Is.True);
            Assert.That(ChannelManager.Instance.Connect(server, NewClientEndPoint()), Is.Null);
        });
    }

    [Test]
    public void UnregisterServer_WithAnUnknownEndPoint_DoesNotThrow()
    {
        Assert.DoesNotThrow(
            () => ChannelManager.Instance.UnregisterServer(NewServerEndPoint()));
    }

    [Test]
    public void ServerEndPointEquality_KeysTheRegistry()
    {
        var name = "cm-equal-" + Guid.NewGuid().ToString("N");
        var registered = new StringEndPoint(name);
        Assert.That(ChannelManager.Instance.RegisterServer(registered, _ => { }), Is.True);
        _servers.Add(registered);

        var equal = new StringEndPoint(name);
        var channel = ChannelManager.Instance.Connect(equal, NewClientEndPoint());

        Assert.That(channel, Is.Not.Null);
    }

    [Test]
    public void Connect_DeliversMessagesInBothDirections()
    {
        var server = Register();
        var channel = ChannelManager.Instance.Connect(server, NewClientEndPoint())!;

        int clientValue = -1;
        int serverValue = -1;
        channel.ClientHandler = message =>
        {
            clientValue = message.Elements[0].Alias.IntValue;
            message.Release();
        };
        channel.ServerHandler = (_, message) =>
        {
            serverValue = message.Elements[0].Alias.IntValue;
            message.Release();
        };

        Assert.Multiple(() =>
        {
            Assert.That(channel.SendToServer(NewMessage(11)), Is.EqualTo(SendResult.Ok));
            Assert.That(channel.SendToClient(NewMessage(22)), Is.EqualTo(SendResult.Ok));
        });
        Assert.Multiple(() =>
        {
            Assert.That(serverValue, Is.EqualTo(11));
            Assert.That(clientValue, Is.EqualTo(22));
        });
    }

    [Test]
    public void ConcurrentConnects_FromDistinctClients_CreateOneChannelEach()
    {
        const int count = 32;
        var server = Register();
        var clients = new IEndPoint[count];
        var channels = new Channel?[count];

        for (int i = 0; i < count; i++)
        {
            clients[i] = NewClientEndPoint();
        }

        Parallel.For(0, count,
            i => channels[i] = ChannelManager.Instance.Connect(server, clients[i]));

        Assert.Multiple(() =>
        {
            Assert.That(channels.All(channel => channel != null), Is.True);
            Assert.That(CreatedCount, Is.EqualTo(count));
        });

        ChannelManager.Instance.UnregisterServer(server);

        Assert.That(channels.All(channel => channel!.IsDisposed), Is.True);
    }

    private IEndPoint Register(Action<Channel>? onCreated = null)
    {
        var server = NewServerEndPoint();
        Action<Channel> callback = channel =>
        {
            lock (_createdLock)
            {
                _created.Add(channel);
            }

            onCreated?.Invoke(channel);
        };

        Assert.That(ChannelManager.Instance.RegisterServer(server, callback), Is.True);
        _servers.Add(server);
        return server;
    }

    private int CreatedCount
    {
        get
        {
            lock (_createdLock)
            {
                return _created.Count;
            }
        }
    }

    private static IEndPoint NewServerEndPoint()
    {
        return new StringEndPoint("cm-server-" + Guid.NewGuid().ToString("N"));
    }

    private static IEndPoint NewClientEndPoint()
    {
        return new StringEndPoint("cm-client-" + Guid.NewGuid().ToString("N"));
    }

    private static UnionDataList NewMessage(int value)
    {
        var message = MemoryRental.Shared.CollectablePool.Acquire<UnionDataList>();
        message.PutLast(value);
        return message;
    }
}
