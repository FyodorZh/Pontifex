using Pontifex.Raw.Reliable;
using Pontifex.StopReasons;
using Pontifex.Utils;

namespace Pontifex.Tests;

/// <summary>
/// Regression tests for server-session teardown tracking.
/// </summary>
[TestFixture]
[Category("TransportTestSupport")]
public sealed class TrackingServerHandlerFactoryTests
{
    /// <summary>
    /// A session wait includes a factory call that was already in flight when the wait began.
    /// </summary>
    [Test]
    public async Task WaitForAllStoppedAsync_WithInFlightFactoryCall_WaitsForTheCreatedHandler()
    {
        var entered = RawReliableTestSupport.CreateCompletionSource();
        var release = RawReliableTestSupport.CreateCompletionSource();
        var factory = new TrackingServerHandlerFactory(new BlockingFactory(entered, release));
        var createTask = Task.Run(() => factory.TryCreate(null!));

        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var waitTask = factory.WaitForAllStoppedAsync(TimeSpan.FromSeconds(5));
            Assert.That(waitTask.IsCompleted, Is.False,
                "an in-flight factory call must prevent the session wait from completing");

            release.TrySetResult();
            var handler = await createTask.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.That(handler, Is.Not.Null);
            Assert.That(waitTask.IsCompleted, Is.False,
                "the created handler has not stopped yet");

            handler!.OnStopped(new UserIntention("test", "complete"));
            await waitTask.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            release.TrySetResult();
        }
    }

    private sealed class BlockingFactory(
        TaskCompletionSource entered,
        TaskCompletionSource release) : IRawReliableServerHandlerFactory<IRawReliableServerHandler>
    {
        public IRawReliableServerHandler? TryCreate(IEndPoint remote)
        {
            entered.TrySetResult();
            release.Task.GetAwaiter().GetResult();
            return new Handler();
        }
    }

    private sealed class Handler : IRawReliableServerHandler
    {
        public void OnConnected(IRawReliableEndpoint endPoint) { }
        public void OnReceived(UnionDataList receivedBuffer) => receivedBuffer.Release();
        public void OnDisconnected(StopReason reason) { }
        public void OnStopped(StopReason reason) { }
    }
}
