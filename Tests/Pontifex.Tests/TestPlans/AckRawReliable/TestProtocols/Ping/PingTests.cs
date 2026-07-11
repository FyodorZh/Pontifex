using Pontifex.Tests;

namespace Pontifex.AckRawReliable.Tests.Ping;

[TestFixtureSource(typeof(AckRawReliableStacks))]
public class PingTests
{
    private readonly ITransportStack _stack;

    public PingTests(ITransportStack stack)
    {
        _stack = stack;
    }

    /// <summary>
    /// Sends 100 concurrent ping requests and verifies each response carries the correct sequence number.
    /// </summary>
    [Test]
    [Category("Fast")]
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
