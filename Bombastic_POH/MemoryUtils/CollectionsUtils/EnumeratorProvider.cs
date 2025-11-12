using System;
using System.Collections;
using System.Collections.Generic;

namespace Shared
{
    public struct EnumeratorProvider<TEnumerator, T>
        where TEnumerator : struct, IEnumerator<T>
    {
        private readonly TEnumerator mEnumerator;

        public EnumeratorProvider(TEnumerator enumerator)
        {
            mEnumerator = enumerator;
        }

        public TEnumerator GetEnumerator()
        {
            return mEnumerator;
        }
    }

    public struct ArrayEnumerable<T>
    {
        private readonly T[] mArray;

        public ArrayEnumerable(T[] array)
        {
            mArray = array;
        }

        public ArrayEnumerator<T> GetEnumerator()
        {
            return new ArrayEnumerator<T>(mArray);
        }

        public T this[int index]
        {
            get { return mArray[index]; }
        }

        public int Count()
        {
            return mArray.Length;
        }
    }


    public struct DowncastEnumerable<T>
    {
        private readonly IEnumerable collection;

        public static DowncastEnumerable<T> Build<TC>(IEnumerable<TC> collection) where TC : T
        {
            return new DowncastEnumerable<T>(collection);
        }

        private DowncastEnumerable(IEnumerable collection)
        {
            this.collection = collection;
        }

        public Enumerator GetEnumerator()
        {
            return new Enumerator(collection.GetEnumerator());
        }


        public struct Enumerator : IEnumerator<T>, IEnumerator
        {
            private readonly IEnumerator enumerator;

            public Enumerator(IEnumerator enumerator)
            {
                this.enumerator = enumerator;
            }

            public void Dispose() {}
            public bool MoveNext() { return enumerator.MoveNext(); }
            public T Current { get { return (T)enumerator.Current; } }

            Object IEnumerator.Current { get { return Current; } }
            void IEnumerator.Reset() { enumerator.Reset(); }
        }
    }
}
