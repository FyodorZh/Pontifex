namespace Pontifex.Tests.NetSockets.LowLevelTcpSocketTests.Harness
{
    /// <summary>
    /// Bounded waiting helpers. The intended-behavior tests may exercise paths that currently hang, so
    /// every await on <c>Completion</c>/<c>DisposeAsync</c> or event must be bounded by a timeout and
    /// surface as a <see cref="TimeoutException"/> instead of deadlocking the test runner.
    /// </summary>
    internal static class TestWait
    {
        public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);

        public static Task<T> GetResultAsync<T>(Task<T> task, TimeSpan? timeout = null)
            => task.WaitAsync(timeout ?? DefaultTimeout);

        public static Task WaitCompletionAsync(Task task, TimeSpan? timeout = null)
            => task.WaitAsync(timeout ?? DefaultTimeout);

        public static Task WaitCompletionAsync(ValueTask task, TimeSpan? timeout = null)
            => task.AsTask().WaitAsync(timeout ?? DefaultTimeout);

        public static bool TrySpinUntil(Func<bool> condition, TimeSpan? timeout = null)
        {
            var limit = timeout ?? DefaultTimeout;
            var timer = System.Diagnostics.Stopwatch.StartNew();
            while (timer.Elapsed < limit)
            {
                if (condition())
                {
                    return true;
                }

                Thread.Sleep(1);
            }

            return condition();
        }

        public static void SpinUntil(Func<bool> condition, TimeSpan? timeout = null, string? message = null)
        {
            if (!TrySpinUntil(condition, timeout))
            {
                throw new TimeoutException(
                    message ?? $"Condition was not satisfied within {timeout ?? DefaultTimeout}.");
            }
        }
    }
}
