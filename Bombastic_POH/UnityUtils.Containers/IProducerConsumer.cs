using Actuarius.Collections;


namespace Shared
{
    /// <summary>
    /// Многопоточный потребитель данных
    /// </summary>
    public interface IConcurrentConsumer<TData> : IConsumer<TData>
    {
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