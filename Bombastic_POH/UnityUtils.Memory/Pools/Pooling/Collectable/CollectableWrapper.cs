using Actuarius.Memory;

namespace Shared.Pooling
{
    public sealed class CollectableWrapper<TObject> : MultiRefCollectable<CollectableWrapper<TObject>>, IMultiRefResourceOwner<TObject>
        where TObject : class
    {
        private TObject mValue;

        public CollectableWrapper()
        {
        }

        public void SetValue(TObject value)
        {
            mValue = value;
        }

        public TObject Resource
        {
            get { return mValue; }
        }

        protected override void OnCollected()
        {
            // DO NOTHING
        }

        protected override void OnRestored()
        {
            // DO NOTHING
        }

        public ReleasableResourceAccessor<TObject> GetAccessor()
        {
            return new ReleasableResourceAccessor<TObject>(mValue, this.Acquire());
        }
    }
}