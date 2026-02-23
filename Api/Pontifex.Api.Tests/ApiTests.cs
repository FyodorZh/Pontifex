using Pontifex.Api.Client;
using Pontifex.Api.Server;
using Pontifex.StopReasons;

namespace Pontifex.Api.Tests;

public class ApiTests
{
    public static (TClient client, TServer server) CreateApi<TClient, TServer>() 
        where TClient : IApiRoot, new() 
        where TServer : IApiRoot, new()
    {
        var inMemoryPipeSystem = new InMemoryPipeSystem();
        inMemoryPipeSystem.Side1.Start();
        inMemoryPipeSystem.Side2.Start();
        
        var client = new TClient();
        var server = new TServer();
        
        client.Start(false, inMemoryPipeSystem.Side1);
        server.Start(true, inMemoryPipeSystem.Side2);
        return (client, server);
    }
    
    [Test]
    public async Task TestClientDisconnect()
    {
        var (client, server) = CreateApi<EmptyApi_Client, EmptyApi_Server>();

        bool clientDisconnecting = false;
        bool serverDisconnecting = false;
        StopReason? clientStopReason = null;
        StopReason? serverStopReason = null;
        
        client.Disconnecting += () => clientDisconnecting = true;
        server.Disconnecting += () => serverDisconnecting = true;
        client.Disconnected += reason => clientStopReason = reason;
        server.Disconnected += reason => serverStopReason = reason;
        
        client.GracefulShutdown(TimeSpan.FromMilliseconds(100));
        await Task.Delay(200);
        Assert.That(clientDisconnecting, Is.True);
        Assert.That(serverDisconnecting, Is.False);
        Assert.That(clientStopReason, Is.TypeOf<UserIntention>());
        Assert.That(serverStopReason, Is.TypeOf<GracefulRemoteIntention>());
    }
    
    [Test]
    public async Task TestServerDisconnect()
    {
        var (client, server) = CreateApi<EmptyApi_Client, EmptyApi_Server>();

        bool clientDisconnecting = false;
        bool serverDisconnecting = false;
        StopReason? clientStopReason = null;
        StopReason? serverStopReason = null;
        
        client.Disconnecting += () => clientDisconnecting = true;
        server.Disconnecting += () => serverDisconnecting = true;
        client.Disconnected += reason => clientStopReason = reason;
        server.Disconnected += reason => serverStopReason = reason;
        
        server.GracefulShutdown(TimeSpan.FromMilliseconds(100));
        await Task.Delay(200);
        Assert.That(clientDisconnecting, Is.False);
        Assert.That(serverDisconnecting, Is.True);
        Assert.That(clientStopReason, Is.TypeOf<GracefulRemoteIntention>());
        Assert.That(serverStopReason, Is.TypeOf<UserIntention>());
    }
    
    [Test]
    public async Task TestApi()
    {
        var (client, server) = CreateApi<BigApi_Client, BigApi_Server>();

        bool clientDisconnecting = false;
        bool serverDisconnecting = false;
        StopReason? clientStopReason = null;
        StopReason? serverStopReason = null;
        
        client.Disconnecting += () => clientDisconnecting = true;
        server.Disconnecting += () => serverDisconnecting = true;
        client.Disconnected += reason => clientStopReason = reason;
        server.Disconnected += reason => serverStopReason = reason;
        
        var x = await client.SubApi.Div.RequestAsync(new Int2() { A = 10, B = 5 });
        Assert.That(x.A, Is.EqualTo(2));
        
        Assert.CatchAsync(async Task () =>
        {
            await client.SubApi.Div.RequestAsync(new Int2() { A = 10, B = 0 });
        });
        
        Assert.That(await client.MultiplyBy2(3), Is.EqualTo(3 * 2));
        
        
        server.GracefulShutdown(TimeSpan.FromMilliseconds(100));
        await Task.Delay(200);
        Assert.That(clientDisconnecting, Is.False);
        Assert.That(serverDisconnecting, Is.True);
        Assert.That(clientStopReason, Is.TypeOf<GracefulRemoteIntention>());
        Assert.That(serverStopReason, Is.TypeOf<UserIntention>());
    }
    
    //
    // [Test]
    // public async Task TestBigApi()
    // {
    //     var inMemoryPipeSystem = new InMemoryPipeSystem();
    //     inMemoryPipeSystem.Side1.Start();
    //     inMemoryPipeSystem.Side2.Start();
    //     
    //     var client = new BigApi_Client();
    //     var server = new BigApi_Server();
    //     
    //     ((IApiRoot)client).Start(false, inMemoryPipeSystem.Side1);
    //     ((IApiRoot)server).Start(true, inMemoryPipeSystem.Side2);
    //
    //     var x = await client.SubApi.Div.RequestAsync(new BigApiSubApi.Request() { A = 10, B = 5 });
    //     Assert.That(x.C, Is.EqualTo(2));
    //     
    //     Assert.CatchAsync(async Task () =>
    //     {
    //         await client.SubApi.Div.RequestAsync(new BigApiSubApi.Request() { A = 10, B = 0 });
    //     });
    //     
    //     Assert.That(await client.MultiplyOnce(3), Is.EqualTo(3 * 2));
    //     
    //     //Assert.That(client.Disconnect.Send(new DisconnectMessage()), Is.EqualTo(SendResult.Ok));
    //     //Assert.That(server.Disconnected, Is.True);
    // }
}