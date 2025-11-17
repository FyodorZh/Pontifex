using Actuarius.Collections;
using Actuarius.Memoria;

namespace Shared
{
    public interface IMacroOwner<TObject> : IMultiRef, IReadOnlyArray<TObject>
    {
    }
}