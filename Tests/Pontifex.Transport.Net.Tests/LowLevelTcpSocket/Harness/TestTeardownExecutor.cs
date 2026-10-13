using Pontifex.NetSockets.Tcp;

namespace Pontifex.Tests.NetSockets.LowLevelTcpSocketTests.Harness
{
    /// <summary>
    /// Deterministic <see cref="ITeardownExecutor"/> for stage tests: queues the connection's single
    /// reserved teardown onto a separate, joined worker that only starts when the test calls
    /// <see cref="Run"/>. No cleanup ever runs on the test thread through this seam.
    /// </summary>
    internal sealed class ManualTeardownExecutor : ITeardownExecutor
    {
        private readonly object _sync = new();
        private readonly ManualResetEventSlim _go = new(false);
        private readonly ManualResetEventSlim _started = new(false);
        private BackgroundAction? _runner;
        private int _queueCount;
        private int _startCount;

        /// <summary>Number of queue requests observed (must be at most one; teardown is reserved once).</summary>
        public int QueueCount => Volatile.Read(ref _queueCount);

        public int StartCount => Volatile.Read(ref _startCount);

        public bool Started => _started.IsSet;

        public void Queue(Action teardown)
        {
            if (Interlocked.Increment(ref _queueCount) != 1)
            {
                throw new InvalidOperationException("The manual teardown executor was queued more than once.");
            }

            var runner = new BackgroundAction(
                () =>
                {
                    _go.Wait();
                    Interlocked.Increment(ref _startCount);
                    _started.Set();
                    teardown();
                },
                "manual-teardown");

            lock (_sync)
            {
                _runner = runner;
            }
        }

        /// <summary>Waits until the queued worker has begun executing the reserved teardown.</summary>
        public bool WaitUntilStarted(TimeSpan timeout) => _started.Wait(timeout);

        /// <summary>Allows the queued worker to run.</summary>
        public void Run() => _go.Set();

        /// <summary>Joins the queued worker (no-op when nothing was queued) and rethrows its exception.</summary>
        public void Join()
        {
            BackgroundAction? runner;
            lock (_sync)
            {
                runner = _runner;
            }

            runner?.Join();
        }
    }
}
