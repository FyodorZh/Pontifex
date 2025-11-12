namespace Shared.Pooling
{
    public sealed class CollectableWrapper<TObject> : MultiRefCollectable<CollectableWrapper<TObject>>, IOwner<TObject>
    {
        private TObject mValue;

        public CollectableWrapper()
        {
        }

        public void SetValue(TObject value)
        {
            mValue = value;
        }

        public TObject Value
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
    }
}