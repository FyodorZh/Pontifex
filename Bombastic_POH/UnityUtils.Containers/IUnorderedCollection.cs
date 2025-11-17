using Fundamentum.Collections;

namespace Shared
{
    /// <summary>
    /// Коробка в которую можно клсать и изымать элементы, порядок изъятия не определён
    /// </summary>
    /// <typeparam name="TData"> Тип элементов </typeparam>
    public interface IUnorderedCollection<TData> : IConsumer<TData>, IProducer<TData>, IStream<TData> // TODO
    {
    }

    /// <summary>
    /// Допускает конкурентное добавление и изъятие элементов двумя потоками.
    /// Один поток кладёт, другой - изымает
    /// </summary>
    public interface ISingleReaderWriterConcurrentUnorderedCollection<TData> : IUnorderedCollection<TData>
    {
    }

    /// <summary>
    /// Допускает полностью асинхронную работу.
    /// </summary>
    public interface IConcurrentUnorderedCollection<TData> : ISingleReaderWriterConcurrentUnorderedCollection<TData>, IConcurrentConsumer<TData>, IConcurrentProducer<TData>
    {
    }
}