using System.Threading;

namespace Shared
{
    public class TinyConcurrentQueue<T> : IConcurrentQueue<T>
    {
        private Node _head;
        private Node _tail;

        public TinyConcurrentQueue()
        {
            _head = new Node(default(T), null);
            _tail = _head;
        }

        public bool Put(T value)
        {
            Node newTail = new Node(value, this);
            Node oldTail = Interlocked.Exchange(ref _tail, newTail);
            oldTail.Next = newTail;
            return true;
        }

        public bool TryPop(out T value)
        {
            Node head = _head;

            while (true)
            {
                TinyConcurrentQueue<T> oQueue = Interlocked.Exchange(ref head.OwnerQueue, null);
                if (oQueue != null)
                {
                    _head = head;
                    value = head.Value;
                    return true;
                }
                if (head.Next == null)
                {
                    _head = head;
                    value = default(T);
                    return false;
                }

                head = head.Next;
            }
        }

        private class Node
        {
            public readonly T Value;
            public TinyConcurrentQueue<T> OwnerQueue;
            public volatile Node Next;

            public Node(T value, TinyConcurrentQueue<T> ownerQueue)
            {
                Value = value;
                OwnerQueue = ownerQueue;
            }
        }

        public System.Collections.Generic.List<T> _DebugElements
        {
            get
            {
                System.Collections.Generic.List<T> res = new System.Collections.Generic.List<T>();
                Node head = _head;
                while (head != null)
                {
                    res.Add(head.Value);
                    head = head.Next;
                }
                return res;
            }
        }
    }
}
