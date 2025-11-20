using Actuarius.Collections;
using Shared.Pooling.ConcurrentBuffered;

namespace Shared.Pooling
{
    public class SmallObjectBufferedPool<TObject> : BufferedPool<TObject>
        where TObject : class
    {
        private class TConstructor : IConstructor<IConcurrentUnorderedCollection<Bucket<TObject>>>
        {
            public IConcurrentUnorderedCollection<Bucket<TObject>> Construct()
            {
                return new TinyConcurrentQueue<Bucket<TObject>>();
            }
        }

        public SmallObjectBufferedPool(IConstructor<TObject> ctor)
            : base(100, 10, ctor, new TConstructor())
        {
        }
    }
}