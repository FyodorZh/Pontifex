using System;
using System.Collections.Generic;

namespace Shared
{
    /// <summary>
    /// Поддерживает хранение множества элементов с одним ключом
    /// </summary>
    public interface IMultiMap<TKey, TData> : IMap<TKey, TData>
        where TKey : IEquatable<TKey>
    {
        bool TryGetValue(MultiMapKey<TKey> key, out TData element);

        IEnumerable<TData> Values { get; }

        IEnumerable<KeyValuePair<MultiMapKey<TKey>, TData>> GetEnumerator();
    }

    /// <summary>
    /// Уникальный и персистентный в рамках лайфтайма мультимапы ключ. Позволяет находить конкретный элемент
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    public struct MultiMapKey<TKey>
    {
        public TKey Key;
        public int Id;

        public MultiMapKey(TKey key, int id)
        {
            Key = key;
            Id = id;
        }
    }
}