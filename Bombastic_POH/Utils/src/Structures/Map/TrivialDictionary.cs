using System;
using System.Collections.Generic;

namespace Shared
{
    public class TrivialDictionary<TKey, TData> : IMap<TKey, TData>
        where TKey : IEquatable<TKey>
    {
        private readonly Dictionary<TKey, TData> mDictionary = new Dictionary<TKey, TData>();

        public bool Add(TKey key, TData element)
        {
            try
            {
                mDictionary.Add(key, element);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool Remove(TKey key)
        {
            return mDictionary.Remove(key);
        }

        public bool TryGetValue(TKey key, out TData element)
        {
            return mDictionary.TryGetValue(key, out element);
        }
    }
}