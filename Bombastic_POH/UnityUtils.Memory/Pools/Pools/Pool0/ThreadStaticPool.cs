using Actuarius.Memory;

namespace Shared.Pooling
{
    public class ThreadStaticPool<TObject> : ThreadSingleton<ThreadStaticPool<TObject>>, IPool<TObject>
        where TObject : class, new()
    {
        public new static readonly IPool<TObject> Instance = new ThreadStaticPool<TObject>();

        private readonly Pool<TObject> mPool = new Pool<TObject>(DefaultConstructor<TObject>.Instance);

        public static TObject Acquire()
        {
            return ThreadSingleton<ThreadStaticPool<TObject>>.Instance.mPool.Acquire();
        }

        public static void Release(TObject obj)
        {
            if (obj != null)
            {
                ThreadSingleton<ThreadStaticPool<TObject>>.Instance.mPool.Release(obj);
            }
        }

        void IPoolSink<TObject>.Release(TObject obj)
        {
            Release(obj);
        }

        TObject IPool<TObject>.Acquire()
        {
            return Acquire();
        }
    }
}