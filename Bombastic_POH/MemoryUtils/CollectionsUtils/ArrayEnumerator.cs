using System;
using System.Collections.Generic;

namespace Shared
{
    [Serializable]
    public struct ArrayEnumerator<T> : IEnumerator<T>, System.Collections.IEnumerator
    {
        private readonly T[] list;
        private int index;
        private T current;

        public ArrayEnumerator(T[] list)
        {
            this.list = list;
            index = 0;
            current = default(T);
        }

        public void Dispose()
        {
        }

        public bool MoveNext()
        {
            T[] localList = list;

            if (index < localList.Length)
            {
                current = localList[index];
                index++;
                return true;
            }
            return MoveNextRare();
        }

        private bool MoveNextRare()
        {
            index = list.Length + 1;
            current = default(T);
            return false;
        }

        public T Current
        {
            get
            {
                return current;
            }
        }

        Object System.Collections.IEnumerator.Current
        {
            get
            {
                if (index == 0 || index == list.Length + 1)
                {
                    throw new InvalidOperationException();
                }
                return Current;
            }
        }

        void System.Collections.IEnumerator.Reset()
        {
            index = 0;
            current = default(T);
        }
    }

    [Serializable]
    public struct ListEnumerator<T> : IEnumerator<T>, System.Collections.IEnumerator
    {
        private readonly IList<T> list;
        private int index;
        private T current;

        public ListEnumerator(IList<T> list)
        {
            this.list = list;
            index = 0;
            current = default(T);
        }

        public void Dispose()
        {
        }

        public bool MoveNext()
        {
            IList<T> localList = list;

            if (index < localList.Count)
            {
                current = localList[index];
                index++;
                return true;
            }
            return MoveNextRare();
        }

        private bool MoveNextRare()
        {
            index = list.Count + 1;
            current = default(T);
            return false;
        }

        public T Current
        {
            get
            {
                return current;
            }
        }

        Object System.Collections.IEnumerator.Current
        {
            get
            {
                if (index == 0 || index == list.Count + 1)
                {
                    throw new InvalidOperationException();
                }
                return Current;
            }
        }

        void System.Collections.IEnumerator.Reset()
        {
            index = 0;
            current = default(T);
        }
    }
}
