using Pontifex.Test;

namespace Pontifex.AckRawReliable.Tests.Ping;

[TestFixtureSource(typeof(AckRawReliableStacks))]
public class PingTests
{
    private readonly TransportStack _stack;

    public PingTests(TransportStack stack)
    {
        _stack = stack;
    }

    [Test]
    public async Task Ping_100_Times()
    {
        var harness = new ApiTestHarness<PingApiClient, PingApiServer>(_stack, true);
        try
        {
            await harness.StartAsync();

            const int count = 100;
            var tasks = new Task<PongResponse>[count];
            for (var i = 0; i < count; i++)
            {
                tasks[i] = harness.ClientApi.SendPing(i);
            }

            var responses = await Task.WhenAll(tasks);

            for (var i = 0; i < count; i++)
            {
                Assert.That(responses[i].Seq, Is.EqualTo(i), $"Response {i} should have Seq={i}");
            }
        }
        finally
        {
            harness.Dispose();
        }
    }
}
