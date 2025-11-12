namespace Shared
{
    public interface IPool<TObject, TParam0> : IPoolSink<TObject>
    {
        /// <summary>
        /// Получить свободный объект из пула
        /// </summary>
        TObject Acquire(TParam0 param0);
    }

    public interface IConcurrentPool<TObject, TParam0> : IPool<TObject, TParam0>, IConcurrentPoolSink<TObject>
    {
    }
}