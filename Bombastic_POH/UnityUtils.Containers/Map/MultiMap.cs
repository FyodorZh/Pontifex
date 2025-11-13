using System;
using System.Collections.Generic;

namespace Shared
{
    public class MultiMap<TKey, TData>: IMultiMap<TKey, TData>
        where TKey : IEquatable<TKey>
    {
        private class Node
        {
            public Node Next;
            public TData Data;
            public int Id;
        }

        private int mNextId;

        private readonly Dictionary<TKey, Node> mTable = new Dictionary<TKey, Node>();
        private Node mPool;

        public bool Add(TKey key, TData element)
        {
            Node node;
            if (mPool != null)
            {
                node = mPool;
                mPool = mPool.Next;
            }
            else
            {
                node = new Node();
            }

            node.Data = element;
            node.Id = mNextId++;

            Node bucket;
            if (mTable.TryGetValue(key, out bucket))
            {
                node.Next = bucket;
                bucket = node;
            }
            else
            {
                bucket = node;
            }
            mTable[key] = bucket;


            return true;
        }

        public bool Remove(TKey key)
        {
            Node bucket;
            if (mTable.TryGetValue(key, out bucket))
            {
                mTable.Remove(key);

                Node last = bucket;
                while (last.Next != null)
                {
                    last = last.Next;
                }

                last.Next = mPool;

                mPool = bucket;
                return true;
            }

            return false;
        }

        public bool TryGetValue(TKey key, out TData element)
        {
            Node bucket;
            if (mTable.TryGetValue(key, out bucket))
            {
                element = bucket.Data;
                return true;
            }

            element = default(TData);
            return true;
        }

        public bool TryGetValue(MultiMapKey<TKey> key, out TData element)
        {
            Node bucket;
            if (!mTable.TryGetValue(key.Key, out bucket))
            {
                element = default(TData);
                return false;
            }

            int id = key.Id;
            while (bucket != null)
            {
                if (bucket.Id == id)
                {
                    element = bucket.Data;
                    return true;
                }

                bucket = bucket.Next;
            }

            element = default(TData);
            return false;
        }

        public IEnumerable<TData> Values
        {
            get
            {
                foreach (var list in mTable.Values)
                {
                    var node = list;
                    while (node != null)
                    {
                        yield return node.Data;
                        node = node.Next;
                    }
                }
            }
        }

        public IEnumerable<KeyValuePair<MultiMapKey<TKey>, TData>> GetEnumerator()
        {
            foreach (var kv in mTable)
            {
                var key = kv.Key;
                var node = kv.Value;
                while (node != null)
                {
                    yield return new KeyValuePair<MultiMapKey<TKey>, TData>(new MultiMapKey<TKey>(key, node.Id), node.Data);
                    node = node.Next;
                }
            }
        }
    }
}