using Actuarius.Collections;

namespace Shared
{
    public interface IMacroOwner<TObject> : IMultiRef, IReadOnlyArray<TObject>
    {
    }
}