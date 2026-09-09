using System;
using System.Threading;
using Actuarius.Collections;
using Actuarius.Concurrent;
using Actuarius.Memory;

namespace Pontifex.Utils
{
    /// <summary>
    /// Defines a unit of work that is processed by a serializing queue: a submitted command is either invoked
    /// or, when it will not be executed, notified of failure.
    /// </summary>
    public interface ICommandBearer
    {
        /// <summary>
        /// Executes the command's main action.
        /// </summary>
        /// <remarks>
        /// Special Considerations:
        /// <list type="bullet">
        /// <item><b>Thread safety:</b> When the command is processed by a serializing queue, Invoke calls are
        /// strictly sequential: one command at a time, never concurrent with another command's Invoke. The thread
        /// performing the invocation is not fixed.</item>
        /// </list>
        /// </remarks>
        void Invoke();

        /// <summary>
        /// Notifies the command that it will not be executed.
        /// </summary>
        /// <remarks>
        /// Special Considerations:
        /// <list type="bullet">
        /// <item><b>Thread safety:</b> No guarantees are made about the calling thread. Fail may be invoked on any
        /// thread and may run concurrently with other commands' invocations.</item>
        /// </list>
        /// </remarks>
        void Fail();
    }
    
    /// <summary>
    /// Serializes the execution of commands. Each submitted command receives an Invoke call strictly sequentially
    /// relative to the others. Release stops new submissions gracefully: commands accepted before the release
    /// barrier are invoked by the shutdown drain, while commands accepted after the barrier are failed instead of
    /// invoked.
    /// </summary>
    /// <typeparam name="TCommand">The type of the command this queue processes; a struct implementing
    /// <see cref="ICommandBearer"/>.</typeparam>
    /// <remarks>
    /// Special Considerations:
    /// <list type="bullet">
    /// <item><b>Thread safety:</b> Schedule, ScheduleFast and Release are safe to call concurrently from any
    /// number of threads. Commands are invoked strictly sequentially, one at a time. Fail and error callbacks
    /// carry no thread guarantees.</item>
    /// <item><b>Preconditions/Postconditions:</b> Release is terminal: after it is called, Schedule and
    /// ScheduleFast return false and drop the command without any callback, and the instance cannot be reused.
    /// </item>
    /// </list>
    /// </remarks>
    public class SerialCommandQueue<TCommand>: IReleasableResource
        where TCommand : struct, ICommandBearer
    {
        private readonly ConcurrentSerializedExecutor _serializedExecutor;
        private readonly IConcurrentQueue<TCommand?> _queue;

        private readonly Action<Exception> _onError;

        private int _state;

        /// <summary>
        /// Initializes a new instance of SerialCommandQueue that processes commands from the specified queue and
        /// reports command failures to the specified error handler.
        /// </summary>
        /// <param name="queue">The concurrent FIFO queue used to store pending commands. This instance keeps a
        /// reference to it; the caller retains ownership.</param>
        /// <param name="onError">The handler invoked with exceptions thrown by a command's Invoke or Fail. It must
        /// be safe for concurrent use and must not throw.</param>
        public SerialCommandQueue(IConcurrentQueue<TCommand?> queue, Action<Exception> onError)
        {
            _onError = onError;
            _serializedExecutor = new ConcurrentSerializedExecutor(OnTick);
            _queue = queue;
        }

        /// <summary>
        /// Submits a command and requests its processing.
        /// </summary>
        /// <param name="command">The command to submit.</param>
        /// <returns>true if the command was accepted; it will then receive exactly one of an Invoke or a Fail
        /// call. false if the underlying queue is full or the queue has been released; the command is dropped
        /// without any callback.</returns>
        /// <remarks>
        /// Special Considerations:
        /// <list type="bullet">
        /// <item><b>Thread safety:</b> Safe to call from any thread, including concurrently with other Schedule,
        /// ScheduleFast and Release calls.</item>
        /// <item><b>Performance:</b> A call that starts a new processing batch executes the entire pending backlog
        /// on the calling thread and may block until the queue is drained.</item>
        /// </list>
        /// </remarks>
        public bool Schedule(TCommand command)
        {
            if (ScheduleFast(command))
            {
                _serializedExecutor.ScheduleOneInvokation();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Submits a command without requesting its processing.
        /// </summary>
        /// <param name="command">The command to submit.</param>
        /// <returns>true if the command was accepted by the queue; false if the underlying queue is full or the
        /// queue has been released, in which case the command is dropped without any callback.</returns>
        /// <remarks>
        /// Special Considerations:
        /// <list type="bullet">
        /// <item><b>Thread safety:</b> Safe to call from any thread, including concurrently with other Schedule,
        /// ScheduleFast and Release calls.</item>
        /// <item><b>Side effects:</b> Unlike Schedule, this method does not request processing and relies on a
        /// later drain to pick the command up. If it observes that Release raced with the submission, it
        /// schedules a single drain so the accepted command is failed instead of left unprocessed. Commands
        /// enqueued before the release barrier are invoked by the shutdown drain; those enqueued after it are
        /// failed.</item>
        /// </list>
        /// </remarks>
        public bool ScheduleFast(TCommand command)
        {
            if ((Volatile.Read(ref _state) == 0) && _queue.Put(command))
            {
                if (Volatile.Read(ref _state) > 0)
                {
                    _serializedExecutor.ScheduleOneInvokation();
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// Releases the queue: stops accepting new commands. Commands accepted before the release barrier are
        /// drained and invoked; commands that arrive after the barrier are failed.
        /// </summary>
        /// <remarks>
        /// Special Considerations:
        /// <list type="bullet">
        /// <item><b>Thread safety:</b> Safe to call concurrently with Schedule, ScheduleFast and other Release
        /// calls; repeated calls are no-ops.</item>
        /// <item><b>Side effects:</b> Terminal. The shutdown drain invokes commands that were accepted before
        /// release and fails commands enqueued after the release barrier. Subsequent Schedule and ScheduleFast
        /// calls return false and drop the command without any callback.</item>
        /// <item><b>Other notes:</b> The shutdown drain may run on a different thread than the caller, so Invoke
        /// or Fail callbacks may still occur after this method returns.</item>
        /// </list>
        /// </remarks>
        public void Release()
        {
            if (Interlocked.CompareExchange(ref _state, 1, 0) == 0)
            {
                bool isOK = false;
                while (!isOK)
                {
                    if (_queue.Put(null))
                    {
                        isOK = true;
                    }
                    else
                    {
                        _serializedExecutor.ScheduleOneInvokation();
                        
                        for (int i = 0; i < 1000; ++i)
                        {
                            if (_queue.Put(null))
                            {
                                isOK = true;
                                break;
                            }
                        }
                    }
                }
                _serializedExecutor.ScheduleOneInvokation();
            }
        }

        private volatile bool _terminatorReached;

        private void OnTick()
        {
            while (_queue.TryPop(out var cmd))
            {
                if (!_terminatorReached)
                {
                    if (cmd.HasValue)
                    {
                        try
                        {
                            cmd.Value.Invoke();
                        }
                        catch (Exception e)
                        {
                            _onError(e);
                        }
                    }
                    else
                    {
                        _terminatorReached = true;
                    }
                }
                else
                {
                    if (cmd.HasValue)
                    {
                        try
                        {
                            cmd.Value.Fail();
                        }
                        catch (Exception e)
                        {
                            _onError(e);
                        }
                    }
                }
            }
        }
    }
}