namespace Shared.Pooling
{
    public class CollectableObjectConcurrentPool<TObject> : CollectableObjectPool<TObject>, IConcurrentPool<TObject>
        where TObject : class, INewCollectable<TObject>, new()
    {
        public CollectableObjectConcurrentPool(int chunkCapacity)
            : base(new DefaultConcurrentPool<TObject>(new QueueBasedConcurrentUnorderedCollection<TObject>(chunkCapacity)))
        {
        }

        public CollectableObjectConcurrentPool(IConcurrentUnorderedCollection<TObject> pool)
            : base(new DefaultConcurrentPool<TObject>(pool))
        {
        }

        public CollectableObjectConcurrentPool(IConcurrentPool<TObject> pool)
            : base(pool)
        {
        }
    }
}