using System.Collections.Generic;
using Pontifex;

namespace Pontifex.Factory.Registry;

public interface ITransportTypeCatalog
{
    IReadOnlyList<TransportTypeInfo> Types { get; }

    bool TryResolve(string name, out TransportTypeInfo? type);

    string GetCanonicalName(TransportType type);
}
