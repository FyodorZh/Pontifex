namespace Shared.Pooling
{
    public abstract class SingleRefCollectable<TSelf> : SingleRefImpl, INewCollectable<TSelf>
        where TSelf : SingleRefCollectable<TSelf>
    {
        private IPoolSink<TSelf> mOwner;

        protected abstract void OnCollected();

        protected abstract void OnRestored();

        protected sealed override void OnReleased()
        {
            mOwner.Release((TSelf)this); // Чтобы убрать этот каст надо использовать ко и контрвариантность
        }

        bool INewCollectable<TSelf>.Collected()
        {
            OnCollected();
            mOwner = null;
            return true;
        }

        void INewCollectable<TSelf>.Restored(IPoolSink<TSelf> pool)
        {
            mOwner = pool;
            Revive();
            OnRestored();
        }
    }
}