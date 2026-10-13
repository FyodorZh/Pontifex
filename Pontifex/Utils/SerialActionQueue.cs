using System;
using Actuarius.Collections;

namespace Pontifex.Utils
{
    /// <summary>
    /// Serializes the execution of submitted actions. Each action is invoked strictly one at a time relative to the
    /// others, regardless of how many threads submit actions concurrently.
    /// </summary>
    /// <remarks>
    /// Special Considerations:
    /// <list type="bullet">
    /// <item><b>Thread safety:</b> Schedule, ScheduleFast and Release are safe to call concurrently from any number
    /// of threads. Actions are invoked strictly sequentially, one at a time, and the thread that runs an action is
    /// not fixed.</item>
    /// </list>
    /// </remarks>
    public class SerialActionQueue : ISerialActionQueue
    {
        private readonly SerialCommandQueue<Job> _commandQueue;

        /// <summary>
        /// Initializes a new instance of <see cref="SerialActionQueue"/> with a bounded capacity.
        /// </summary>
        /// <param name="capacity">The maximum number of actions that may be pending at once; must be at least 1.
        /// A value of 0 makes every submission fail, and a negative value is invalid.</param>
        /// <param name="onError">The handler invoked with exceptions thrown by a submitted action. It must be
        /// non-null, safe for concurrent use, and must not throw.</param>
        public SerialActionQueue(int capacity, Action<Exception> onError)
        {
            _commandQueue = new SerialCommandQueue<Job>(new LimitedConcurrentQueue<Job?>(capacity), onError);
        }
        
        /// <summary>
        /// Initializes a new instance of <see cref="SerialActionQueue"/> with unbounded capacity.
        /// </summary>
        /// <param name="onError">The handler invoked with exceptions thrown by a submitted action. It must be
        /// non-null, safe for concurrent use, and must not throw.</param>
        public SerialActionQueue(Action<Exception> onError)
        {
            _commandQueue = new SerialCommandQueue<Job>(new SynchronizedBySpinLockConcurrentQueue<Job?>(new SystemQueue<Job?>()), onError);
        }
        
        /// <summary>
        /// Submits a typed action for processing without requesting that the queue be drained.
        /// </summary>
        /// <typeparam name="T">The type of the argument passed to the action; a reference type.</typeparam>
        /// <param name="param">The argument passed to the action when it is invoked.</param>
        /// <param name="action">The action to invoke with <paramref name="param"/>. Must be non-null.</param>
        /// <param name="onFailToRun">The callback invoked with <paramref name="param"/> if the action is accepted
        /// but will not be invoked; may be <see langword="null"/>, in which case the action is dropped silently.
        /// </param>
        /// <returns><see langword="true"/> if the action was accepted; <see langword="false"/> if the queue is
        /// full or has been released, in which case the action is dropped without invoking
        /// <paramref name="onFailToRun"/>.</returns>
        /// <remarks>
        /// Special Considerations:
        /// <list type="bullet">
        /// <item><b>Thread safety:</b> Safe to call from any thread, including concurrently with Schedule,
        /// ScheduleFast and Release.</item>
        /// <item><b>Side effects:</b> This method does not request processing; the action is picked up by a later
        /// drain, for example one triggered by Schedule or Release.</item>
        /// </list>
        /// </remarks>
        public bool ScheduleFast<T>(T param, Action<T> action, Action<T>? onFailToRun)
            where T : class
        {
            var wrapper = WrapperAccessor<T>.Value;
            return _commandQueue.ScheduleFast(new Job()
            {
                Runner = wrapper, 
                OnRun = action,
                OnFailToRun = onFailToRun,
                Param = param
            });
        }
        
        /// <summary>
        /// Submits a typed action and requests that the queue be processed.
        /// </summary>
        /// <typeparam name="T">The type of the argument passed to the action; a reference type.</typeparam>
        /// <param name="param">The argument passed to the action when it is invoked.</param>
        /// <param name="action">The action to invoke with <paramref name="param"/>. Must be non-null.</param>
        /// <param name="onFailToRun">The callback invoked with <paramref name="param"/> if the action is accepted
        /// but will not be invoked; may be <see langword="null"/>, in which case the action is dropped silently.
        /// </param>
        /// <returns><see langword="true"/> if the action was accepted; <see langword="false"/> if the queue is
        /// full or has been released, in which case the action is dropped without invoking
        /// <paramref name="onFailToRun"/>.</returns>
        /// <remarks>
        /// Special Considerations:
        /// <list type="bullet">
        /// <item><b>Thread safety:</b> Safe to call from any thread, including concurrently with Schedule,
        /// ScheduleFast and Release.</item>
        /// <item><b>Performance:</b> If this call starts a new processing batch, it runs the entire pending
        /// backlog on the calling thread and may block until the queue is drained.</item>
        /// </list>
        /// </remarks>
        public bool Schedule<T>(T param, Action<T> action, Action<T>? onFailToRun)
            where T: class
        {
            var wrapper = WrapperAccessor<T>.Value;
            return _commandQueue.Schedule(new Job()
            {
                Runner = wrapper, 
                OnRun = action,
                OnFailToRun = onFailToRun,
                Param = param
            });
        }
        
        /// <summary>
        /// Submits a parameterless action for processing without requesting that the queue be drained.
        /// </summary>
        /// <param name="action">The action to invoke. Must be non-null.</param>
        /// <returns><see langword="true"/> if the action was accepted; <see langword="false"/> if the queue is
        /// full or has been released, in which case the action is dropped without any callback.</returns>
        /// <remarks>
        /// Special Considerations:
        /// <list type="bullet">
        /// <item><b>Thread safety:</b> Safe to call from any thread, including concurrently with Schedule,
        /// ScheduleFast and Release.</item>
        /// <item><b>Side effects:</b> This method does not request processing; the action is picked up by a later
        /// drain, for example one triggered by Schedule or Release. If the queue is released before the action is
        /// run, the action is dropped silently.</item>
        /// </list>
        /// </remarks>
        public bool ScheduleFast(Action action)
        {
            return ScheduleFast(action, a => a.Invoke(), null);
        }

        /// <summary>
        /// Submits a parameterless action and requests that the queue be processed.
        /// </summary>
        /// <param name="action">The action to invoke. Must be non-null.</param>
        /// <returns><see langword="true"/> if the action was accepted; <see langword="false"/> if the queue is
        /// full or has been released, in which case the action is dropped without any callback.</returns>
        /// <remarks>
        /// Special Considerations:
        /// <list type="bullet">
        /// <item><b>Thread safety:</b> Safe to call from any thread, including concurrently with Schedule,
        /// ScheduleFast and Release.</item>
        /// <item><b>Performance:</b> If this call starts a new processing batch, it runs the entire pending
        /// backlog on the calling thread and may block until the queue is drained.</item>
        /// </list>
        /// </remarks>
        public bool Schedule(Action action)
        {
            return Schedule(action, a => a.Invoke(), null);
        }

        /// <summary>
        /// Releases the queue, stopping it from accepting new actions.
        /// </summary>
        /// <remarks>
        /// Special Considerations:
        /// <list type="bullet">
        /// <item><b>Thread safety:</b> Safe to call concurrently with Schedule, ScheduleFast and other Release
        /// calls; repeated calls are no-ops.</item>
        /// <item><b>Side effects:</b> Terminal. Actions accepted before the release barrier are invoked; actions
        /// that were accepted but will not be invoked are passed to their onFailToRun callback if one was
        /// supplied. Subsequent Schedule and ScheduleFast calls return <see langword="false"/> and drop the action
        /// without invoking any callback.</item>
        /// <item><b>Other notes:</b> The drain may run on a different thread than the caller, so action and
        /// onFailToRun callbacks may still occur after this method returns. The instance cannot be reused.</item>
        /// </list>
        /// </remarks>
        public void Release()
        {
            _commandQueue.Release();
        }

        private delegate void Wrapper(object action, object parameter);

        private static class WrapperAccessor<T>
        {
            public static readonly Wrapper Value = (action, parameter) =>
            {
                Action<T> typedAction = (Action<T>)action;
                T typedParam = (T)parameter;
                typedAction.Invoke(typedParam);
            };
        }

        private struct Job : ICommandBearer
        {
            public Wrapper Runner;
            public object OnRun;
            public object? OnFailToRun;
            public object Param;

            public void Invoke()
            {
                Runner(OnRun, Param);
            }

            public void Fail()
            {
                if (OnFailToRun != null)
                {
                    Runner(OnFailToRun, Param);
                }
            }
        }
    }
}
