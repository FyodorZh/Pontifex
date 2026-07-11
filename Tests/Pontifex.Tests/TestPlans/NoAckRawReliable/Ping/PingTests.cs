using Actuarius.Memory;
using Pontifex.NoAck.Raw;
using Pontifex.StopReasons;
using Pontifex.Tests;
using Pontifex.Utils;

namespace Pontifex.NoAckRawReliable.Tests.Ping;

[TestFixtureSource(typeof(NoAckRawReliableStacks))]
public class PingTests
{
    private readonly ITransportStack _stack;

    public PingTests(ITransportStack stack)
    {
        _stack = stack;
    }

    [Test]
    public async Task Ping_100_Times()
    {
        var memory = TransportRegistry.Memory;
        var factory = _stack.GetTransportFactory();

        var server = (INoAckRawReliableServer)factory.BuildServer();
        var client = (INoAckRawReliableClient)factory.BuildClient();

        try
        {
            var responseTcs = new TaskCompletionSource[100];
            var responses = new int[100];

            client.OnReceived += data =>
            {
                using var disposer = data.AsDisposable();
                if (data.TryPopFirst(out int seq))
                {
                    responses[seq] = seq;
                    responseTcs[seq].SetResult();
                }
            };

            server.OnReceived += (endpoint, data) =>
            {
                using var disposer = data.AsDisposable();
                if (data.TryPopFirst(out int seq))
                {
                    var echo = memory.CollectablePool.Acquire<UnionDataList>();
                    echo.PutFirst(seq);
                    server.Send(endpoint, echo);
                }
            };

            for (var i = 0; i < responseTcs.Length; i++)
            {
                responseTcs[i] = new TaskCompletionSource();
            }

            var serverStopped = new TaskCompletionSource<bool>();
            server.Start(_ => serverStopped.TrySetResult(true));

            var clientStopped = new TaskCompletionSource<bool>();
            client.Start(_ => clientStopped.TrySetResult(true));

            for (var i = 0; i < 100; i++)
            {
                var msg = memory.CollectablePool.Acquire<UnionDataList>();
                msg.PutFirst(i);
                client.Send(msg);
            }

            for (var i = 0; i < 100; i++)
            {
                await responseTcs[i].Task.WaitAsync(TimeSpan.FromSeconds(5));
            }

            for (var i = 0; i < 100; i++)
            {
                Assert.That(responses[i], Is.EqualTo(i), $"Response {i} should have Seq={i}");
            }
        }
        finally
        {
            client.Stop(new UserIntention("test", "done"));
            server.Stop(new UserIntention("test", "done"));
        }
    }
}
