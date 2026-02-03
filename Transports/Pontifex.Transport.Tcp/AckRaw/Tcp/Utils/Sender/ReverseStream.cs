using System;
using System.Diagnostics.CodeAnalysis;
using Actuarius.Collections;

namespace Pontifex.Transports.Tcp
{
    public class InverseDelegateProducer<T> : IProducer<T>
    {
        private readonly Action<IConsumer<T>> _requester;
        private readonly IQueue<T> _queue = new CycleQueue<T>();
        
        public InverseDelegateProducer(Action<IConsumer<T>> requester)
        {
            _requester = requester;
        }
        
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