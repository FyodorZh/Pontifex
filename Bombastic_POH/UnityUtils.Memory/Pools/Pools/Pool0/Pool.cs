using Actuarius.Collections;
using Actuarius.Memory;

namespace Shared.Pooling
{
    public class Pool<TObject> : IPool<TObject>
        where TObject : class
    {
        private readonly IConstructor<TObject> mConstructor;
        private readonly IStream<TObject> mPool;

        public Pool(IConstructor<TObject> ctor)
            : this(ctor, new CycleQueue<TObject>())
        {
        }

        public Pool(IConstructor<TObject> ctor, IStream<TObject> pool)
        {
            mConstructor = ctor;
            mPool = pool;
        }

        public void Release(TObject obj)
        {
            if (obj != null)
            {
                mPool.Put(obj);
            }
        }

        public TObject Acquire()
        {
            if (mPool.TryPop(out var obj))
            {
                return obj;
            }
            return mConstructor.Construct();
        }
    }

    public class DefaultPool<TObject> : Pool<TObject>
        where TObject : class, new()
    {
        public DefaultPool()
            : base(DefaultConstructor<TObject>.Instance)
        {
        }

    }
}