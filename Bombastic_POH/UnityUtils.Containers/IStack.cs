namespace Shared
{
    /// <summary>
    /// Элементы изымаются в порядке LIFO
    /// </summary>
    /// <typeparam name="TData"></typeparam>
    public interface IStack<TData> : IUnorderedCollection<TData>
    {
    }

    public interface IConcurrentStack<TData> : IStack<TData>, IConcurrentUnorderedCollection<TData>
    {
    }
}