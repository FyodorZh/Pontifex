using System;
using System.Diagnostics.CodeAnalysis;
using Actuarius.Collections;

namespace Pontifex.Raw.Reliable.Ack.Tcp
{
    /// <summary>
    /// An <see cref="IProducer{T}"/> that produces elements on demand: when its internal queue is empty, it invokes
    /// a caller-supplied requester callback that is expected to enqueue one or more elements.
    /// </summary>
    /// <typeparam name="T">The type of elements produced.</typeparam>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Thread safety:</b> The type is not thread-safe and performs no internal synchronization.
    /// Any thread may call <see cref="TryPop"/>, but the caller must ensure that calls never overlap.</description></item>
    /// </list>
    /// </remarks>
    public class InverseDelegateProducer<T> : IProducer<T>
    {
        private readonly Action<IConsumer<T>> _requester;
        private readonly IQueue<T> _queue = new CycleQueue<T>();

        /// <summary>
        /// Initializes a new instance with an empty internal queue and the given requester callback.
        /// </summary>
        /// <param name="requester">The callback invoked with the internal queue, viewed as an
        /// <see cref="IConsumer{T}"/>, whenever <see cref="TryPop"/> finds the queue empty. Must not be
        /// <see langword="null"/>.</param>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Preconditions/Postconditions:</b> The callback must enqueue elements synchronously
        /// during the invocation and must not retain the provided consumer for later use.</description></item>
        /// </list>
        /// </remarks>
        public InverseDelegateProducer(Action<IConsumer<T>> requester)
        {
            _requester = requester;
        }

        /// <summary>
        /// Returns the next queued element, refilling the internal queue on demand when it is empty.
        /// </summary>
        /// <param name="value">When this method returns <see langword="true"/>, contains the next element; otherwise,
        /// the default value of <typeparamref name="T"/>.</param>
        /// <returns><see langword="true"/> if an element was available or the requester provided one; otherwise,
        /// <see langword="false"/>.</returns>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description><b>Thread safety:</b> Not thread-safe; concurrent calls must be serialized by the
        /// caller.</description></item>
        /// <item><description><b>Side effects:</b> When the internal queue is empty, this method invokes the requester
        /// callback exactly once, passing the internal queue as an <see cref="IConsumer{T}"/>. The callback runs
        /// synchronously on the calling thread.</description></item>
        /// <item><description><b>Preconditions/Postconditions:</b> A <see langword="false"/> result means the queue was
        /// empty and the single callback invocation added nothing. The callback must not call <see cref="TryPop"/>
        /// again, directly or indirectly.</description></item>
        /// </list>
        /// </remarks>
        public bool TryPop([MaybeNullWhen(false)] out T value)
        {
            if (!_queue.TryPop(out value))
            {
                _requester.Invoke(_queue);
                return _queue.TryPop(out value);
            }

            return true;
        }
    }
}