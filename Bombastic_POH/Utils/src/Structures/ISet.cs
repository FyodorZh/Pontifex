namespace Shared
{
    public interface ISet<TData> : IConsumer<TData>
    {
        bool Remove(TData element);
        bool Contains(TData element);
    }

    public interface IConcurrentSet<TData> : ISet<TData>, IConcurrentConsumer<TData>
    {
    }
}
