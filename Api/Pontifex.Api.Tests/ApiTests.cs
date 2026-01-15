using Pontifex.Api;
using Pontifex.Api.Client;
using Pontifex.Api.Server;

namespace Pontifex.Api.Tests;

public class ApiTests
{
    [Test]
    public void TestEmptyApi()
    {
        var inMemoryPipeAllocator = new InMemoryPipeSystem();

        var client = new EmptyApi_Client();
        var server = new EmptyApi_Server();
        
        client.Prepare(false, inMemoryPipeAllocator.Side1);
        server.Prepare(true, inMemoryPipeAllocator.Side2);
        
        Assert.That(client.Disconnect.Name, Is.EqualTo("Disconnect"));
        Assert.That(server.Disconnect.Name, Is.EqualTo("Disconnect"));
        
        Assert.That(client.Disconnect.Send(new DisconnectMessage()), Is.EqualTo(SendResult.Ok));
        Assert.That(server.Disconnected, Is.True);
    }
    
    [Test]
    public async Task TestBigApi()
    {
        var inMemoryPipeAllocator = new InMemoryPipeSystem();

        var client = new BigApi_Client();
        var server = new BigApi_Server();
        
        client.Prepare(false, inMemoryPipeAllocator.Side1);
        server.Prepare(true, inMemoryPipeAllocator.Side2);

        var x = await client.SubApi.Div.RequestAsync(new BigApiSubApi.Request() { A = 10, B = 5 });
        Assert.That(x.C, Is.EqualTo(2));
        
        Assert.CatchAsync(async Task () =>
        {
            await client.SubApi.Div.RequestAsync(new BigApiSubApi.Request() { A = 10, B = 0 });
        });
        
        Assert.That(await client.MultiplyOnce(3), Is.EqualTo(3 * 2));
        
        Assert.That(client.Disconnect.Send(new DisconnectMessage()), Is.EqualTo(SendResult.Ok));
        Assert.That(server.Disconnected, Is.True);
    }
}