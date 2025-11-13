namespace Shared
{
    /// <summary>
    /// Потребитель данных
    /// </summary>
    public interface IConsumer<TData>
    {
        /// <summary>
        /// Кладёт элемент в приёмник.
        /// </summary>
        /// <param name="value"></param>
        /// <returns> FALSE если положить элемент не удалось. Например, исчерпана ёмкость коллекции или что угодно другое </returns>
        bool Put(TData value);
    }

    /// <summary>
    /// Многопоточный потребитель данных
    /// </summary>
    public interface IConcurrentConsumer<TData> : IConsumer<TData>
    {
    }

    /// <summary>
    /// Источник данных
    /// </summary>
    public interface IProducer<TData>
    {
        /// <summary>
        /// Получает очередной элемент из продюсера
        /// </summary>
        /// <param name="value"> возвращаемый элемент </param>
        /// <returns> FALSE если очередной элемент получить не удалось </returns>
        bool TryPop(out TData value);
    }

    /// <summary>
    /// Многопоточный источник данных
    /// </summary>
    public interface IConcurrentProducer<TData> : IProducer<TData>
    {
    }

    public static class Ext_IProducer
    {
        public static void ForAll<TData>(this IProducer<TData> self, System.Action<TData> processor)
        {
            if (self != null)
            {
                TData value;
                while (self.TryPop(out value))
                {
                    processor(value);
                }
            }
        }

        public static AsEnumerable<TObject> Enumerate<TObject>(this IProducer<TObject> producer)
        {
            return new AsEnumerable<TObject>(producer);
        }

        public struct AsEnumerable<TObject>
        {
            private readonly IProducer<TObject> mProducer;

            public AsEnumerable(IProducer<TObject> producer)
            {
                mProducer = producer;
            }

            public Enumerator<TObject> GetEnumerator()
            {
                return new Enumerator<TObject>(mProducer);
            }
        }

        public struct Enumerator<TObject>// : IEnumerator<TObject>
        {
            private readonly IProducer<TObject> mProducer;
            private TObject mCurrent;

            public Enumerator(IProducer<TObject> producer)
            {
                mProducer = producer;
                mCurrent = default(TObject);
            }

            public bool MoveNext()
            {
                return mProducer.TryPop(out mCurrent);
            }

            public TObject Current
            {
                get { return mCurrent; }
            }
        }
    }
}