using Actuarius.Memory;

namespace Shared.Pooling
{
    public abstract class MultiRefCollectable<TSelf> : MultiRefResource, ICollectable<TSelf>
        where TSelf : MultiRefCollectable<TSelf>
    {
        private IPoolSink<TSelf> mOwner;

        protected MultiRefCollectable()
            : base(true)
        {
        }

        protected abstract void OnCollected();
        protected abstract void OnRestored();

        protected sealed override void OnReleased()
        {
            mOwner.Release((TSelf)this); // Чтобы убрать этот каст надо использовать ко и контрвариантность
        }

        bool ICollectable<TSelf>.Collected()
        {
            OnCollected();
            mOwner = null;
            return true;
        }

        void ICollectable<TSelf>.Restored(IPoolSink<TSelf> pool)
        {
            mOwner = pool;
            Revive();
            OnRestored();
        }
    }
}