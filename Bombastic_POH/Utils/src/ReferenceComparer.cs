using System.Collections.Generic;
using System.Runtime.CompilerServices;


public class ReferenceComparer<T> : IEqualityComparer<T>
{
    bool IEqualityComparer<T>.Equals(T x, T y)
    {
        return ReferenceEquals(x, y);
    }

    int IEqualityComparer<T>.GetHashCode(T obj)
    {
        return RuntimeHelpers.GetHashCode(obj);
    }
}
