using Shared.Pooling;

namespace Shared.Pool
{
    public abstract class NewCollectable<TSelf> : SingleRefCollectable<TSelf>, System.IDisposable
        where TSelf : NewCollectable<TSelf>
    {
        public void Dispose()
        {
            Release();
        }
    }
}