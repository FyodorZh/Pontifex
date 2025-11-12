using System;

namespace Shared
{
    public interface IMap<TKey, TData>
        //where TKey : IEquatable<TKey>
    {
        bool Add(TKey key, TData element);
        bool Remove(TKey key);
        bool TryGetValue(TKey key, out TData element);
    }

    public interface IConcurrentMap<TKey, TData> : IMap<TKey, TData>
        //where TKey : IEquatable<TKey>
    {
    }
}