using Pontifex.Api;
using Pontifex.Api.Client;
using Pontifex.Api.Tests.InMemoryApi.Harness;
using Pontifex.StopReasons;

namespace Pontifex.Api.Tests.InMemoryApi;

[TestFixture]
[Category("Pontifex.Api.InMemoryApi")]
public sealed class InMemoryApiTests
{
    private static (TClient client, TServer server) CreateApi<TClient, TServer>()
        where TClient : IApiRoot, new()
        where TServer : IApiRoot, new()
    {
        var pipeSystem = new InMemoryPipeSystem();
        pipeSystem.Side1.Start();
        pipeSystem.Side2.Start();

        var client = new TClient();
        var server = new TServer();

        client.Start(false, pipeSystem.Side1);
        server.Start(true, pipeSystem.Side2);
        return (client, server);
    }

    [Test]
    public void GracefulShutdown_FromClient_StopsBothEndsWithExpectedReasons()
    {
        var (client, server) = CreateApi<EmptyApiClient, EmptyApiServer>();

        var clientDisconnectingCount = 0;
        var serverDisconnectingCount = 0;
        StopReason? clientStopReason = null;
        StopReason? serverStopReason = null;

        client.Disconnecting += () => clientDisconnectingCount++;
        server.Disconnecting += () => serverDisconnectingCount++;
        client.Disconnected += reason => clientStopReason = reason;
        server.Disconnected += reason => serverStopReason = reason;

        client.GracefulShutdown(null);

        Assert.Multiple(() =>
        {
            Assert.That(clientDisconnectingCount, Is.EqualTo(1));
            Assert.That(serverDisconnectingCount, Is.Zero);
            Assert.That(clientStopReason, Is.TypeOf<UserIntention>());
            Assert.That(serverStopReason, Is.TypeOf<GracefulRemoteIntention>());
        });
    }

    [Test]
    public void GracefulShutdown_FromServer_StopsBothEndsWithExpectedReasons()
    {
        var (client, server) = CreateApi<EmptyApiClient, EmptyApiServer>();

        var clientDisconnectingCount = 0;
        var serverDisconnectingCount = 0;
        StopReason? clientStopReason = null;
        StopReason? serverStopReason = null;

        client.Disconnecting += () => clientDisconnectingCount++;
        server.Disconnecting += () => serverDisconnectingCount++;
        client.Disconnected += reason => clientStopReason = reason;
        server.Disconnected += reason => serverStopReason = reason;

        server.GracefulShutdown(null);

        Assert.Multiple(() =>
        {
            Assert.That(clientDisconnectingCount, Is.Zero);
            Assert.That(serverDisconnectingCount, Is.EqualTo(1));
            Assert.That(clientStopReason, Is.TypeOf<GracefulRemoteIntention>());
            Assert.That(serverStopReason, Is.TypeOf<UserIntention>());
        });
    }

    [Test]
    public void Api_RequestResponseAndMessageCalls_ReturnExpectedResultsAndShutdownPropagates()
    {
        var (client, server) = CreateApi<BigApiClient, BigApiServer>();

        var clientDisconnectingCount = 0;
        var serverDisconnectingCount = 0;
        StopReason? clientStopReason = null;
        StopReason? serverStopReason = null;

        client.Disconnecting += () => clientDisconnectingCount++;
        server.Disconnecting += () => serverDisconnectingCount++;
        client.Disconnected += reason => clientStopReason = reason;
        server.Disconnected += reason => serverStopReason = reason;

        IRequestSuccess<Int1>? success = null;
        IRequestFail? failure = null;
        client.SubApi.Div.Request(
            new Int2 { A = 10, B = 5 },
            response => success = response,
            error => failure = error);

        Assert.Multiple(() =>
        {
            Assert.That(success, Is.Not.Null);
            Assert.That(success!.Response.A, Is.EqualTo(2));
            Assert.That(failure, Is.Null);
        });

        success = null;
        failure = null;
        client.SubApi.Div.Request(
            new Int2 { A = 10, B = 0 },
            response => success = response,
            error => failure = error);

        Assert.Multiple(() =>
        {
            Assert.That(success, Is.Null);
            Assert.That(failure, Is.Not.Null);
            Assert.That(failure!.Error, Is.EqualTo("Division by zero"));
            Assert.That(client.MultiplyBy2(3), Is.EqualTo(6));
        });

        server.GracefulShutdown(null);

        Assert.Multiple(() =>
        {
            Assert.That(clientDisconnectingCount, Is.Zero);
            Assert.That(serverDisconnectingCount, Is.EqualTo(1));
            Assert.That(clientStopReason, Is.TypeOf<GracefulRemoteIntention>());
            Assert.That(serverStopReason, Is.TypeOf<UserIntention>());
        });
    }
}
