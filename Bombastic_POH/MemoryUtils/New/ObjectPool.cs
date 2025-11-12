using Shared.Pooling;

namespace Shared.Pool
{
    public class NewObjectPool<TObject> : ThreadSingleton<NewObjectPool<TObject>>, IPool<TObject>
        where TObject : class, INewCollectable<TObject>, new()
    {
        private readonly IPool<TObject> mPool = new Pool<TObject>(DefaultConstructor<TObject>.Instance);

        public void Release(TObject obj)
        {
            if (obj != null)
            {
                if (obj.Collected())
                {
                    mPool.Release(obj);
                }
            }
        }

        public TObject Acquire()
        {
            TObject obj = mPool.Acquire();
            obj.Restored(this);
            return obj;
        }
    }
}