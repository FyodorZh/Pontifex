using Actuarius.Collections;
using Actuarius.Memory;

namespace Shared
{
    public interface IMacroOwner<TObject> : IMultiRefResource, IReadOnlyArray<TObject>
    {
    }
}