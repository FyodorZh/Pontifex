using System.Runtime.ExceptionServices;

namespace Pontifex.Tests.NetSockets.LowLevelTcpSocketTests.Harness
{
    /// <summary>
    /// Small helpers for the lifecycle tests that genuinely need two logical threads (callback-in-flight,
    /// dispose-vs-pending, policy-vs-dispose). Exceptions from the background action are captured and
    /// rethrown on the joining thread.
    /// </summary>
    internal static class Threading
    {
        public static void RunAndJoin(Action action, TimeSpan? timeout = null)
        {
            var background = new BackgroundAction(action);
            background.Join(timeout);
        }
    }

    internal sealed class BackgroundAction
    {
        private readonly Thread _thread;
        private readonly ManualResetEventSlim _completed = new(false);
        private Exception? _exception;

        public BackgroundAction(Action action, string? name = null)
        {
            _thread = new Thread(() =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    _exception = ex;
                }
                finally
                {
                    _completed.Set();
                }
            })
            {
                IsBackground = true,
                Name = name ?? "test-background",
            };
            _thread.Start();
        }

        public bool Completed => _completed.IsSet;

        public Exception? Exception => _exception;

        /// <summary>Waits for completion and rethrows any captured exception.</summary>
        public void Join(TimeSpan? timeout = null)
        {
            if (!_completed.Wait(timeout ?? TestWait.DefaultTimeout))
            {
                throw new TimeoutException("Background action did not complete within the timeout.");
            }

            if (_exception != null)
            {
                ExceptionDispatchInfo.Capture(_exception).Throw();
            }
        }

        /// <summary>Waits for completion without rethrowing; inspect <see cref="Exception"/> afterwards.</summary>
        public bool JoinRaw(TimeSpan? timeout = null) => _completed.Wait(timeout ?? TestWait.DefaultTimeout);
    }
}
