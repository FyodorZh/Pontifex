using Actuarius.Memory;

namespace Shared.Pooling
{
    public class CollectableObjectPool<TObject> : IPool<TObject>
        where TObject : class, ICollectableResource<TObject>
    {
        private readonly IPool<TObject> mPool;

        public CollectableObjectPool(IConstructor<TObject> ctor)
            : this(new DelegatePool<TObject>(ctor.Construct))
        {
        }

        public CollectableObjectPool(IPool<TObject> pool)
        {
            mPool = pool;
        }

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