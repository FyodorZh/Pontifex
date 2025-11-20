using Actuarius.Collections;

namespace Shared.Pooling
{
    public class ConcurrentPool<TObject> : Pool<TObject>, IConcurrentPool<TObject>
        where TObject : class
    {
        public ConcurrentPool(IConstructor<TObject> ctor, int chunkCapacity)
            : this(ctor, new QueueBasedConcurrentUnorderedCollection<TObject>(chunkCapacity))
        {
        }

        public ConcurrentPool(IConstructor<TObject> ctor, IConcurrentUnorderedCollection<TObject> pool)
            : base(ctor, pool)
        {
        }
    }

    public class DefaultConcurrentPool<TObject> : ConcurrentPool<TObject>
        where TObject : class, new()
    {
        public DefaultConcurrentPool(IConcurrentUnorderedCollection<TObject> pool)
            : base(DefaultConstructor<TObject>.Instance, pool)
        {
        }
    }
}