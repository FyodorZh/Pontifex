namespace Shared
{
    public interface IPool<TObject> : IPoolSink<TObject>
    {
        /// <summary>
        /// Получить свободный объект из пула
        /// </summary>
        TObject Acquire();
    }

    public interface IThreadStaticPool<TObject> : IPool<TObject>
    {
    }

    public interface IConcurrentPool<TObject> : IPool<TObject>, IConcurrentPoolSink<TObject>
    {
    }
}