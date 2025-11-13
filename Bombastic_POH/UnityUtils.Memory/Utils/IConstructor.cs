namespace Shared
{
    public interface IConstructor<TObject>
    {
        /// <summary>
        /// Тредобезопасное конструирование нового инстанса объекта
        /// </summary>
        TObject Construct();
    }

    public interface IConstructor<TObject, TParam1>
    {
        /// <summary>
        /// Тредобезопасное конструирование нового инстанса объекта
        /// </summary>
        TObject Construct(TParam1 param1);
    }

    /// <summary>
    /// Создаёт объект на основе дефолтного конструктора
    /// </summary>
    public class DefaultConstructor<TObject> : IConstructor<TObject>
        where TObject : new()
    {
        public static readonly DefaultConstructor<TObject> Instance = new DefaultConstructor<TObject>();
        public TObject Construct()
        {
            return new TObject();
        }

        private DefaultConstructor()
        {
        }
    }
}