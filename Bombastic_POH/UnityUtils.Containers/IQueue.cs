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

    public interface ISingleReaderWriterConcurrentQueue_old<TData> : IQueue_old<TData>, ISingleReaderWriterConcurrentUnorderedCollection<TData>
    {
    }

    public interface IConcurrentQueue_old<TData> : ISingleReaderWriterConcurrentQueue_old<TData>, IConcurrentUnorderedCollection<TData>
    {
    }
}
