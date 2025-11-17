using Fundamentum.Collections;

namespace Shared
{
    public interface IConcurrentStack<TData> : IStack<TData>, IConcurrentUnorderedCollection<TData>
    {
    }
}