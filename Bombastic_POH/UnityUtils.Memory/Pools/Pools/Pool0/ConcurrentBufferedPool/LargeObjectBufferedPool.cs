using Actuarius.Collections;
using Shared.Pooling.ConcurrentBuffered;

namespace Shared.Pooling
{
    public class LargeObjectBufferedPool<TObject> : BufferedPool<TObject>
        where TObject : class
    {
        private class TConstructor : IConstructor<IConcurrentUnorderedCollection<Bucket<TObject>>>
        {
            public IConcurrentUnorderedCollection<Bucket<TObject>> Construct()
            {
                return new TinyConcurrentQueue<Bucket<TObject>>();
            }
        }

        public LargeObjectBufferedPool(IConstructor<TObject> ctor)
            : base(20, 3, ctor, new TConstructor())
        {
        }
    }
}