using System;
using System.Collections.Generic;

namespace Shared
{
    public class ListHelper<T> : ThreadSingleton<ListHelper<T>>
        where T : IEquatable<T>
    {
        private T mValue;
        private bool Compare(T value)
        {
            return mValue.Equals(value);
        }

        public static int RemoveAllThatEqualTo(List<T> list, T value)
        {
            var self = Instance;
            self.mValue = value;
            return list.RemoveAll(self.Compare);
        }
    }
}
