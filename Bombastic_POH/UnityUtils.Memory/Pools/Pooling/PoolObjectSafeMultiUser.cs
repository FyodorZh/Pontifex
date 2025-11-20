using Actuarius.Memory;

namespace Shared
{
    public class PoolObjectSafeMultiUser<TPoolObject> : MultiRefResource
    {
        protected readonly IPoolSink<TPoolObject> mPool;
        protected TPoolObject mObject;

        protected PoolObjectSafeMultiUser(IPoolSink<TPoolObject> pool, TPoolObject objectFromPool)
            : base(true)
        {
            mPool = pool;
            mObject = objectFromPool;
        }

        protected override void OnReleased()
        {
            mPool.Release(mObject);
            mObject = default(TPoolObject);
        }
    }
}