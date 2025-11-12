using Shared.Pooling;

namespace Shared.Pool
{
    public abstract class NewMultiRefCollectable<TSelf> : MultiRefCollectable<TSelf>
        where TSelf : NewMultiRefCollectable<TSelf>
    {
    }
}