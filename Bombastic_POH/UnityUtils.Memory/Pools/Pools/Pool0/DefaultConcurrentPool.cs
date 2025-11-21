using Actuarius.Collections;
using Actuarius.Memory;

namespace Shared.Pooling
{
    public class DefaultConcurrentPool<TResource> : ConcurrentDelegatePool<TResource>
        where TResource : class, new()
    {
        public DefaultConcurrentPool(IConcurrentUnorderedCollection<TResource> pool)
            : base(() => new(), pool)
        {
        }
    }
}