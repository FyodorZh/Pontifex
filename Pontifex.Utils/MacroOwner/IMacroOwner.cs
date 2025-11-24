using Actuarius.Collections;

namespace Actuarius.Memory
{
    public interface IMacroOwner<out TObject> : IMultiRefResource, IReadOnlyArray<TObject>
    {
    }
}