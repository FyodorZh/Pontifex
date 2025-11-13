namespace Shared
{
    /// <summary>
    /// Элементы изымаются в порядке добавления
    /// </summary>
    /// <typeparam name="TData"></typeparam>
    public interface IQueue<TData> : IUnorderedCollection<TData>
    {
    }

    public interface ISingleReaderWriterConcurrentQueue<TData> : IQueue<TData>, ISingleReaderWriterConcurrentUnorderedCollection<TData>
    {
    }

    public interface IConcurrentQueue<TData> : ISingleReaderWriterConcurrentQueue<TData>, IConcurrentUnorderedCollection<TData>
    {
    }
}
