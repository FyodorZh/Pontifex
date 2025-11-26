using Actuarius.Memory;

namespace Shared.Pooling
{
    public sealed class CollectableOwner<TObject> : MultiRefCollectableResource<CollectableOwner<TObject>>, IMultiRefResourceOwner<TObject>
        where TObject : class, IReleasableResource
    {
        private TObject mValue;

        public CollectableOwner()
        {
        }

        public bool SetValue(TObject value)
        {
            if (IsAlive)
            {
                if (mValue != null)
                {
                    mValue.Release();
                }

                mValue = value;
                return true;
            }

            if (value != null)
            {
                value.Release();
            }
            return false;
        }

        public TObject Resource
        {
            get { return mValue; }
        }

        protected override void OnCollected()
        {
            if (mValue != null)
            {
                mValue.Release();
                mValue = null;
            }
        }

        protected override void OnRestored()
        {
            // DO NOTHING
        }

        public TObject ShowResourceUnsafe(out TObject resource)
        {
            return resource = Resource;
        }
    }
}