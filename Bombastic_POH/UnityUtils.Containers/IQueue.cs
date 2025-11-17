using Actuarius.Collections;

namespace Shared
{
    /// <summary>
    /// Элементы изымаются в порядке добавления
    /// </summary>
    /// <typeparam name="TData"></typeparam>
    public interface IQueue_old<TData> : IStream<TData>, IUnorderedCollection<TData>
    {
    }

    public interface ISingleReaderWriterConcurrentQueue<TData> : IQueue_old<TData>, ISingleReaderWriterConcurrentUnorderedCollection<TData>
    {
    }

    public interface IConcurrentQueue<TData> : ISingleReaderWriterConcurrentQueue<TData>, IConcurrentUnorderedCollection<TData>
    {
    }
}
