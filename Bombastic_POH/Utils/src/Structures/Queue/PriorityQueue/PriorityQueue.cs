using System;
using System.Collections.Generic;
namespace Shared
{
    /// <summary>
    /// Приоритетная очередь. Чем меньше ключ, тем он приоритетнее
    /// </summary>
    public partial class PriorityQueue<Key, Data> : IQueue<KeyValuePair<Key, Data>>
        where Key : IComparable<Key>
    {
        private readonly List<Key> mKeys = new List<Key>();
        private readonly List<Data> mData = new List<Data>();

        public PriorityQueue()
        {
            Clear();
        }
        
        public void Clear()
        {
            mKeys.Clear();
            mData.Clear();
            mKeys.Add(default(Key));
            mData.Add(default(Data));
        }

        public int Count
        {
            get
            {
                return mKeys.Count - 1;
            }
        }

        public void Enqueue(Key key, Data data)
        {
            mKeys.Add(key);
            mData.Add(data);
            Up(mKeys.Count - 1);
        }

        public Key TopKey()
        {
            if (Count != 0)
            {
                return mKeys[1];
            }
            return default(Key);
        }

        public Data Peek()
        {
            if (Count != 0)
            {
                return mData[1];
            }
            return default(Data);
        }

        public Data Dequeue()
        {
            KeyValuePair<Key, Data> res;
            TryPop(out res);
            return res.Value;
        }

        public bool Put(KeyValuePair<Key, Data> value)
        {
            Enqueue(value.Key, value.Value);
            return true;
        }

        public bool TryPop(out KeyValuePair<Key, Data> value)
        {
            int count = Count;
            if (count != 0)
            {
                value = new KeyValuePair<Key, Data>(mKeys[1], mData[1]);
                mKeys[1] = mKeys[count];
                mData[1] = mData[count];
                mKeys.RemoveAt(count);
                mData.RemoveAt(count);
                Down(1);
                return true;
            }
            value = default(KeyValuePair<Key, Data>);
            return false;
        }

        public Enumerable Enumerate(QueueEnumerationOrder order)
        {
            return new Enumerable(this, order);
        }

        public DataEnumerable Values
        {
            get
            {
                return new DataEnumerable(mData);
            }
        }        
        
        Key IPriorityQueueCtl.KeyAt(int idx)
        {
            return mKeys[idx];
        }

        Data IPriorityQueueCtl.DataAt(int idx)
        {
            return mData[idx];
        }

        bool IPriorityQueueCtl.RemoveAtIndex(int id)
        {
            if (id < 1 || id >= mKeys.Count)
            {
                return false;
            }

            Swap(id, mKeys.Count - 1);
            mKeys.RemoveAt(mKeys.Count - 1);
            mData.RemoveAt(mData.Count - 1);

            Down(id);
            return true;
        }

        bool IPriorityQueueCtl.UpdateKeyAtIndex(int id, Key key)
        {
            if (id < 1 || id >= mKeys.Count)
            {
                return false;
            }

            int cmp = mKeys[id].CompareTo(key);
            mKeys[id] = key;
            if (cmp < 0)
            {
                Down(id);
            }
            else if (cmp > 0)
            {
                Up(id);
            }
            return true;
        }

        private void Up(int id)
        {
            while (id > 1)
            {
                int top = id / 2;
                if (mKeys[id].CompareTo(mKeys[top]) < 0)
                {
                    Swap(id, top);
                    id = top;
                }
                else
                {
                    break;
                }
            }
        }

        private void Down(int id)
        {
            int count = Count;
            while (id <= count)
            {
                int a = id * 2;
                int minAB = id;
                if (a <= count)
                {
                    int b = a + 1;
                    if (b <= count)
                    {
                        minAB = mKeys[a].CompareTo(mKeys[b]) < 0 ? a : b;
                    }
                    else
                    {
                        minAB = a;
                    }
                }
                if (minAB != id && mKeys[minAB].CompareTo(mKeys[id]) < 0)
                {
                    Swap(minAB, id);
                    id = minAB;
                }
                else
                {
                    break;
                }
            }
        }

        private void Swap(int a, int b)
        {
            Key k = mKeys[a];
            Data d = mData[a];
            mKeys[a] = mKeys[b];
            mData[a] = mData[b];
            mKeys[b] = k;
            mData[b] = d;
        }
    }
}
