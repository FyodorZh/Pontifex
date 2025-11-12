using System;
using System.Collections.Generic;

namespace Shared
{
    /// <summary>
    /// Тривиальная реализация словаря через список.Полностью дублирует поведение стандартного Dictionary
    /// - Быстрая на малом числе элементов (штук 5-10)
    /// - Не аллоцирует в рамках Capacity
    /// - foreach не аллоцирует в Unity
    /// </summary>
    public class TinyDictionary<TKey, TValue> : IMap<TKey, TValue>
        where TKey : struct, IEquatable<TKey>
    {
        private int mCount = 0;
        private int mCapacity = 10;

        private TKey[] mKeys = new TKey[10];
        private KeyValuePair<TKey, TValue>[] mList = new KeyValuePair<TKey, TValue>[10];

        public int Count
        {
            get { return mCount; }
        }

        public void Clear()
        {
            mCount = 0;
        }

        public TValue this[TKey key]
        {
            get
            {
                for (int i = 0; i < mCount; ++i)
                {
                    if (key.Equals(mKeys[i]))
                    {
                        return mList[i].Value;
                    }
                }
                throw new KeyNotFoundException();
            }
            set
            {
                for (int i = 0; i < mCount; ++i)
                {
                    if (key.Equals(mKeys[i]))
                    {
                        mKeys[i] = key;
                        mList[i] = new KeyValuePair<TKey, TValue>(key, value);
                        return;
                    }
                }

                if (mCount == mCapacity)
                {
                    Grow();
                }
                mKeys[mCount] = key;
                mList[mCount++] = new KeyValuePair<TKey, TValue>(key, value);
            }
        }

        bool IMap<TKey, TValue>.Add(TKey key, TValue value)
        {
            for (int i = 0; i < mCount; ++i)
            {
                if (key.Equals(mKeys[i]))
                {
                    return false;
                }
            }

            if (mCount == mCapacity)
            {
                Grow();
            }
            mKeys[mCount] = key;
            mList[mCount++] = new KeyValuePair<TKey, TValue>(key, value);
            return true;
        }

        public void Add(TKey key, TValue value)
        {
            if (!((IMap<TKey, TValue>)this).Add(key, value))
            {
                throw new ArgumentException();
            }
        }

        public bool ContainsKey(TKey key)
        {
            for (int i = 0; i < mCount; ++i)
            {
                if (key.Equals(mKeys[i]))
                {
                    return true;
                }
            }
            return false;
        }

        public bool Remove(TKey key)
        {
            for (int i = 0; i < mCount; ++i)
            {
                if (key.Equals(mKeys[i]))
                {
                    mCount -= 1;
                    mKeys[i] = mKeys[mCount];
                    mList[i] = mList[mCount];
                    return true;
                }
            }
            return false;
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            for (int i = 0; i < mCount; ++i)
            {
                if (key.Equals(mKeys[i]))
                {
                    value = mList[i].Value;
                    return true;
                }
            }
            value = default(TValue);
            return false;
        }

        public Enumerator GetEnumerator()
        {
            return new Enumerator(mList, mCount);
        }

        private void Grow()
        {
            int newCapacity = mCapacity * 2;
            TKey[] newKeys = new TKey[newCapacity];
            KeyValuePair<TKey, TValue>[] newList = new KeyValuePair<TKey, TValue>[newCapacity];
            for (int i = 0; i < mCapacity; ++i)
            {
                newKeys[i] = mKeys[i];
                newList[i] = mList[i];
            }
            mCapacity = newCapacity;
            mKeys = newKeys;
            mList = newList;
        }

        public struct Enumerator : IEnumerator<KeyValuePair<TKey, TValue>>
        {
            private readonly KeyValuePair<TKey, TValue>[] mList;
            private readonly int mLength;

            private int index;
            private KeyValuePair<TKey, TValue> current;

            public Enumerator(KeyValuePair<TKey, TValue>[] list, int length)
            {
                mList = list;
                mLength = length;
                index = 0;
                current = default(KeyValuePair<TKey, TValue>);
            }

            public void Dispose()
            {
            }

            public bool MoveNext()
            {
                KeyValuePair<TKey, TValue>[] localList = mList;

                if (index < mLength)
                {
                    current = localList[index];
                    index++;
                    return true;
                }
                return MoveNextRare();
            }

            private bool MoveNextRare()
            {
                index = mLength + 1;
                current = default(KeyValuePair<TKey, TValue>);
                return false;
            }

            public KeyValuePair<TKey, TValue> Current
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
                    if (index == 0 || index == mList.Length + 1)
                    {
                        throw new InvalidOperationException();
                    }
                    return Current;
                }
            }

            void System.Collections.IEnumerator.Reset()
            {
                index = 0;
                current = default(KeyValuePair<TKey, TValue>);
            }
        }
    }
}
