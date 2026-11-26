using System;
using System.Threading;
using System.Threading.Tasks;
using Actuarius.Memory;
using Pontifex.Endpoints;
using Pontifex.RR.Unreliable;
using Pontifex.RR.Unreliable.Direct;
using Pontifex.Tests.RR.RRUnreliableDirect.Harness;
using Pontifex.Utils;
using Scriba;

namespace Pontifex.Tests.RR.RRUnreliableDirect;

/// <summary>
/// Integration stop-race fixture for the RRUnreliable Direct client. The send is gated, so the interleaving is
/// deterministic even though it uses a task.
/// </summary>
[TestFixture]
[Category("Direct.RRUnreliable")]
[Category("Direct.RRUnreliable.Integration")]
public sealed class RRUnreliableDirectStopRaceIntegrationTests
{
    private static readonly ILogger Logger = new Logger([]);

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        Logger.Dispose();
    }

    [Test]
    public async Task RequestRacingStop_ReturnsErrorAndUnbindsTheHandler()
    {
        var client = new RRUnreliableDirectClient(
            new StringEndPoint("rr-stop-race-" + Guid.NewGuid().ToString("N")),
            TimeSpan.FromSeconds(3),
            Logger,
            MemoryRental.Shared);
        using var entered = new ManualResetEventSlim(false);
        using var release = new ManualResetEventSlim(false);
        client.BeforeSendForTests = () =>
        {
            entered.Set();
            release.Wait(TimeSpan.FromSeconds(5));
        };
        var handler = new RecordingClientHandler();
        Assert.That(client.Start(_ => { }), Is.True);

        var request = Task.Run(() => client.Request(TestMessages.Int(1), handler));
        Assert.That(entered.Wait(TimeSpan.FromSeconds(5)), Is.True);

        Assert.That(client.Stop(), Is.True);
        release.Set();

        Assert.That(await request.WaitAsync(TimeSpan.FromSeconds(5)), Is.Not.EqualTo(SendResult.Ok));
        Assert.Multiple(() =>
        {
            Assert.That(handler.IsBound, Is.False);
            Assert.That(client.PendingCountForTests, Is.Zero);
        });
    }
}
