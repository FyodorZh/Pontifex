using System;

namespace Pontifex.Utils
{
    /// <summary>
    /// Serializes the execution of submitted actions: each action is invoked strictly one at a time relative to
    /// all others, regardless of how many threads submit concurrently. Actions submitted while a drain is in
    /// progress are processed on a later iteration of that same serial batch.
    /// </summary>
    /// <remarks>
    /// Special Considerations:
    /// <list type="bullet">
    /// <item><b>Thread safety:</b> Schedule is safe to call concurrently from any number of threads. Actions are
    /// invoked strictly sequentially, one at a time, and the thread that runs an action is not fixed.</item>
    /// </list>
    /// </remarks>
    public interface ISerialActionQueue
    {
        /// <summary>
        /// Submits a parameterless action and requests that the queue be processed.
        /// </summary>
        /// <param name="action">The action to invoke. Must be non-null.</param>
        /// <returns><see langword="true"/> if the action was accepted; otherwise <see langword="false"/>, in
        /// which case the action is dropped.</returns>
        bool Schedule(Action action);

        /// <summary>
        /// Submits a typed action and requests that the queue be processed.
        /// </summary>
        /// <typeparam name="T">The type of the argument passed to the action; a reference type.</typeparam>
        /// <param name="param">The argument passed to the action when it is invoked.</param>
        /// <param name="action">The action to invoke with <paramref name="param"/>. Must be non-null.</param>
        /// <param name="onFailToRun">The callback invoked with <paramref name="param"/> if the action is accepted
        /// but will not be invoked; may be <see langword="null"/>, in which case the action is dropped
        /// silently.</param>
        /// <returns><see langword="true"/> if the action was accepted; otherwise <see langword="false"/>, in
        /// which case the action is dropped without invoking <paramref name="onFailToRun"/>.</returns>
        bool Schedule<T>(T param, Action<T> action, Action<T>? onFailToRun) where T : class;
    }
}
