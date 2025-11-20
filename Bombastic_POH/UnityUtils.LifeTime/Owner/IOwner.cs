using Actuarius.Memory;

namespace Shared
{
    public interface IOwner<TObject> : IMultiRefResource
    {
        TObject Value { get; }
    }
}