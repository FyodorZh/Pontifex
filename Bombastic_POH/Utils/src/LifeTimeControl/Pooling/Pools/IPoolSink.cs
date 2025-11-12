namespace Shared
{
    public interface IPoolSink<TObject>
    {
        /// <summary>
        /// Вернуть объект в пул.
        /// </summary>
        /// <param name="obj"> Можно возвращать default(TObject) </param>
        void Release(TObject obj);
    }

    public interface IConcurrentPoolSink<TObject> : IPoolSink<TObject>
    {
    }
}