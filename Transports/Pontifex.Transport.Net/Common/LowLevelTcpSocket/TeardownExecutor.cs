using System;
using System.Threading;

namespace Pontifex.NetSockets.Tcp
{
    /// <summary>
    /// Internal seam for executing a connection's single reserved physical teardown on a worker
    /// instead of the requesting thread. The production adapter queues to the shared thread pool;
    /// tests substitute a manually started executor. Not public API.
    /// </summary>
    internal interface ITeardownExecutor
    {
        /// <summary>
        /// Queues the connection's reserved teardown work. Must not block and must not be relied on
        /// for ordering beyond "eventually runs"; the connection reserves the work exactly once.
        /// </summary>
        void Queue(Action teardown);
    }

    /// <summary>
    /// Production <see cref="ITeardownExecutor"/>: one thread-pool work item per connection
    /// teardown. There is no dedicated OS thread per connection and no worker per chunk; the
    /// connection caches its teardown delegate, so queueing does not allocate a per-chunk closure.
    /// </summary>
    internal sealed class ThreadPoolTeardownExecutor : ITeardownExecutor
    {
        public static readonly ThreadPoolTeardownExecutor Instance = new();

        public void Queue(Action teardown)
            => ThreadPool.QueueUserWorkItem(static state => ((Action)state!).Invoke(), teardown);
    }
}
