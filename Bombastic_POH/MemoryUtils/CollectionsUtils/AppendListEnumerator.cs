using System;
using System.Collections.Generic;

namespace Shared
{
    [Serializable]
    public struct AppendListEnumerator<T>
    {
        private readonly List<T> list;
        private int index;
        private T current;

        public AppendListEnumerator(List<T> list)
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
            List<T> localList = list;

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
    }
}
