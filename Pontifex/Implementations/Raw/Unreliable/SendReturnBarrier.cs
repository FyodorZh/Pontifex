using System;
using System.Threading;

namespace Pontifex.Raw.Unreliable
{
    /// <summary>
    /// Tracks how many <c>Send</c> calls are in flight so a delivery worker can defer a callback until the
    /// sending call has returned.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Other notes</b>: The barrier is per endpoint. The reentrancy depth is per instance
    /// and per thread, so a nested send on a second endpoint of the same thread does not inherit the depth of
    /// this barrier and cannot bypass it.</description></item>
    /// <item><description><b>Thread safety</b>: <see cref="Enter"/>, <see cref="Exit"/>, and <see cref="Wait"/>
    /// are safe for concurrent callers.</description></item>
    /// </list>
    /// </remarks>
    internal sealed class SendReturnBarrier : IDisposable
    {
        private readonly object _gate = new object();
        private readonly ThreadLocal<int> _depth = new ThreadLocal<int>(() => 0);
        private int _inFlight;

        /// <summary>
        /// Gets the number of in-flight send entries; diagnostic and test seam.
        /// </summary>
        internal int InFlightForTests => Volatile.Read(ref _inFlight);

        /// <summary>
        /// Registers the start of a <c>Send</c> call on the calling thread.
        /// </summary>
        public void Enter()
        {
            lock (_gate)
            {
                int depth = _depth.Value + 1;
                _depth.Value = depth;
                if (depth == 1)
                {
                    _inFlight++;
                }
            }
        }

        /// <summary>
        /// Registers the return of a <c>Send</c> call on the calling thread.
        /// </summary>
        public void Exit()
        {
            lock (_gate)
            {
                int depth = _depth.Value - 1;
                _depth.Value = depth;

                if (depth <= 0)
                {
                    _inFlight--;
                    Monitor.PulseAll(_gate);
                }
            }
        }

        /// <summary>
        /// Blocks until no <c>Send</c> call other than the caller's own reentrant calls is in flight.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Other notes</b>: A no-op when no send is in flight. The caller's own reentrancy
        /// depth is excluded so a handler that sends synchronously from a callback cannot deadlock against
        /// itself.</description></item>
        /// </list>
        /// </remarks>
        public void Wait()
        {
            lock (_gate)
            {
                int ownDepth = _depth.Value;
                while (_inFlight > ownDepth)
                {
                    Monitor.Wait(_gate);
                }
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            _depth.Dispose();
        }
    }
}
