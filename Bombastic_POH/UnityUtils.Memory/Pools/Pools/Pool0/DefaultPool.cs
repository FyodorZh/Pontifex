using Actuarius.Collections;
using Actuarius.Memory;

namespace Shared.Pooling
{
    public class DefaultPool<TResource> : DelegatePool<TResource>
        where TResource : class, new()
    {
        public DefaultPool()
            : base(() => new())
        {
        }

    }
}